using System.ComponentModel;
using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// Cards: create, change, move and delete them, assign people, label, comment, snooze and repeat them. Every member
/// of the card's board (or its project) can. Each real change is written to the card's activity as yours, and open
/// boards update right away.
/// </summary>
[Route(Constants.Api.RoutePrefix + "/cards")]
[Tags("Cards")]
[ProblemResponse(StatusCodes.Status404NotFound)]
public class CardsController(IDbContextFactory<DataContext> dbContextFactory, CardService cardService) : ApiControllerBase
{
    /// <summary>Get a card</summary>
    /// <remarks>
    /// Everything about one card: description, assignees, labels, subtasks, comments (newest first), reminders,
    /// attachments, links, time records and repeat.
    /// </remarks>
    /// <param name="id">The card's id.</param>
    /// <response code="200">The card.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CardDetailDTO>> Get(int id)
    {
        // A card the caller can't access gets the same 404 as a missing one, so ids of other boards' cards don't leak.
        var detail = await LoadDetailAsync(id);

        if (detail is null)
            return NotFound();

        return detail;
    }

    /// <summary>Create a card</summary>
    /// <remarks>
    /// Adds a card to the column, at the top or bottom as the column's settings say. Only the name is required; you
    /// can also set the description, priority, deadline, labels and assignees in the same call.
    /// </remarks>
    /// <response code="201">The new card.</response>
    /// <response code="400">
    /// The name is missing, blank or longer than 300 characters, or a label or assignee isn't the board's.
    /// </response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The column doesn't exist, or you aren't a member of its board or project.</response>
    /// <response code="409">The column already holds its maximum number of cards.</response>
    [HttpPost]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status409Conflict)]
    [ProducesResponseType<CardDetailDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CardDetailDTO>> Create([FromBody] CreateCardRequest request)
    {
        var result = await cardService.CreateCardAsync(request.ColumnId, UserId, new(request.Name)
        {
            Description = request.Description,
            Priority = request.Priority,
            Deadline = request.Deadline,
            LabelIds = request.LabelIds ?? [],
            AssigneeIds = request.AssigneeIds ?? [],
        });

        if (!result.Succeeded)
            return Failure(result);

        var detail = await LoadDetailAsync(result.Value!.Id);
        await NotifyBoardsAsync(detail!.BoardId);

        return CreatedAtAction(nameof(Get), new { id = detail.Id }, detail);
    }

    /// <summary>Update a card</summary>
    /// <remarks>
    /// Changes only the fields you send; leave a field out (or send null) to keep it. To remove the deadline, send
    /// `"clearDeadline": true`. Each real change is written to the card's activity log, and open boards update.
    /// </remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="request">The fields to change.</param>
    /// <response code="200">The card after the changes.</response>
    /// <response code="400">The name is blank or too long, or a value isn't valid (e.g. an unknown priority).</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPatch("{id:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CardDetailDTO>> Update(int id, [FromBody] UpdateCardRequest request)
    {
        // Each of these does nothing when the value doesn't change, so the activity log only shows real changes.
        var changes = new List<Func<Task<OperationResult>>>();

        if (request.Name is not null)
            changes.Add(() => cardService.RenameAsync(id, UserId, request.Name));

        if (request.Description is not null)
            changes.Add(() => cardService.UpdateDescriptionAsync(id, UserId, request.Description));

        if (request.Priority is { } priority)
            changes.Add(() => cardService.SetPriorityAsync(id, UserId, priority));

        if (request.ClearDeadline == true)
            changes.Add(() => cardService.RemoveDeadlineAsync(id, UserId));
        else if (request.Deadline is { } deadline)
            changes.Add(() => cardService.SetDeadlineAsync(id, UserId, deadline));

        if (request.Flagged is { } flagged)
            changes.Add(() => cardService.SetFlaggedAsync(id, UserId, flagged));

        return await ApplyAsync(id, changes);
    }

    /// <summary>Delete a card</summary>
    /// <remarks>Deletes the card with its subtasks, comments, attachments, time records and links. This can't be undone.</remarks>
    /// <param name="id">The card's id.</param>
    /// <response code="204">The card is deleted.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpDelete("{id:int}")]
    [ApiWrite]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(int id)
    {
        using var db = dbContextFactory.CreateDbContext();
        var boardId = await db.Cards.Where(x => x.Id == id).Select(x => x.Column.BoardId).FirstOrDefaultAsync();
        var result = await cardService.DeleteAsync(id, UserId);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(boardId);
        return NoContent();
    }

    /// <summary>Move a card</summary>
    /// <remarks>
    /// Moves the card to a column on its board, before `beforeCardId` or at the bottom. Give the card's own column to
    /// reorder it there. Moving to another column is written to the card's activity.
    /// </remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="request">Where to move it.</param>
    /// <response code="200">The card in its new place.</response>
    /// <response code="400">The target column isn't on the card's board.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    /// <response code="409">The target column is full.</response>
    [HttpPost("{id:int}/move")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status409Conflict)]
    public Task<ActionResult<CardDetailDTO>> Move(int id, [FromBody] MoveCardRequest request) =>
        ApplyAsync(id, [() => cardService.MoveAsync(id, UserId, request.TargetColumnId, request.BeforeCardId)]);

    /// <summary>Get a card's activity</summary>
    /// <remarks>
    /// What happened to the card, newest first, a page at a time: pass the response's `nextBefore` as `before` to get
    /// older entries.
    /// </remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="before">Only entries older than this activity id, from the previous page's `nextBefore`.</param>
    /// <param name="limit">How many entries to return, at most 200.</param>
    /// <param name="type">Only entries of this kind, e.g. `CommentPosted`.</param>
    /// <response code="200">A page of activity.</response>
    /// <response code="400">The limit isn't between 1 and 200, or `before` isn't an activity of this card.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpGet("{id:int}/activity")]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActivityPageDTO>> Activity(
        int id,
        [FromQuery] int? before,
        [FromQuery, DefaultValue(50)] int limit = 50,
        [FromQuery] ActivityType? type = null
    )
    {
        if (!await cardService.CanAccessCardAsync(id, UserId))
            return NotFound();

        using var db = dbContextFactory.CreateDbContext();
        return await ActivityPageAsync(db.Activities.Where(x => x.CardId == id), before, limit, type);
    }

    #region Assignees and labels

    /// <summary>Assign someone</summary>
    /// <remarks>
    /// Assigns a member of the board to the card; use your own id (see `GET /me`) to join it. Assigning someone who
    /// already is changes nothing.
    /// </remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="userId">The member's user id, from the board's members.</param>
    /// <response code="200">The card's assignees.</response>
    /// <response code="400">They aren't a member of the board or its project.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPut("{id:int}/assignees/{userId:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<IEnumerable<CardAssigneeDTO>>> Assign(int id, int userId) => SetAssigneeAsync(id, userId, true);

    /// <summary>Unassign someone</summary>
    /// <remarks>Takes the person off the card; use your own id to leave it. Unassigning someone who isn't changes nothing.</remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="userId">The assignee's user id.</param>
    /// <response code="200">The card's assignees.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpDelete("{id:int}/assignees/{userId:int}")]
    [ApiWrite]
    public Task<ActionResult<IEnumerable<CardAssigneeDTO>>> Unassign(int id, int userId) => SetAssigneeAsync(id, userId, false);

    /// <summary>Add a label</summary>
    /// <remarks>Puts one of the board's labels on the card. Adding one the card has changes nothing.</remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="labelId">The label's id, from the board.</param>
    /// <response code="200">The card's labels.</response>
    /// <response code="400">The label isn't one of the board's.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPut("{id:int}/labels/{labelId:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<IEnumerable<LabelDTO>>> AddLabel(int id, int labelId) => SetLabelAsync(id, labelId, true);

    /// <summary>Remove a label</summary>
    /// <remarks>Takes the label off the card. Removing one the card doesn't have changes nothing.</remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="labelId">The label's id.</param>
    /// <response code="200">The card's labels.</response>
    /// <response code="400">The label isn't one of the board's.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpDelete("{id:int}/labels/{labelId:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<IEnumerable<LabelDTO>>> RemoveLabel(int id, int labelId) => SetLabelAsync(id, labelId, false);

    #endregion

    /// <summary>Comment on a card</summary>
    /// <remarks>Adds a comment as you. Open boards update right away.</remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="request">The comment.</param>
    /// <response code="201">The new comment.</response>
    /// <response code="400">The text is missing, blank or longer than 10,000 characters.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPost("{id:int}/comments")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<CommentDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CommentDTO>> AddComment(int id, [FromBody] AddCommentRequest request)
    {
        var result = await cardService.AddCommentAsync(id, UserId, request.Text);

        if (!result.Succeeded)
            return Failure(result);

        using var db = dbContextFactory.CreateDbContext();

        var created = await db.Comments
            .Where(x => x.Id == result.Value!.Id)
            .Select(ApiMappings.CommentProjection)
            .FirstAsync();

        await NotifyCardBoardsAsync(id);
        return CreatedAtAction(nameof(Get), new { id }, created);
    }

    #region Snooze and repeat

    /// <summary>Snooze a card</summary>
    /// <remarks>Hides the card from the board until the time; then it comes back on its own.</remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="request">Until when.</param>
    /// <response code="200">The card.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPut("{id:int}/snooze")]
    [ApiWrite]
    public Task<ActionResult<CardDetailDTO>> Snooze(int id, [FromBody] SnoozeCardRequest request) =>
        ApplyAsync(id, [() => cardService.SnoozeAsync(id, UserId, request.Until)]);

    /// <summary>Unsnooze a card</summary>
    /// <remarks>Shows the card on the board again now.</remarks>
    /// <param name="id">The card's id.</param>
    /// <response code="200">The card.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpDelete("{id:int}/snooze")]
    [ApiWrite]
    public Task<ActionResult<CardDetailDTO>> Unsnooze(int id) =>
        ApplyAsync(id, [() => cardService.UnsnoozeAsync(id, UserId)]);

    /// <summary>Make a card repeat</summary>
    /// <remarks>
    /// On the schedule, a copy of the card is created in the target column. Replaces the card's current repeat. Give
    /// the fields its type needs: `weekDays` for `WeekDays`, `monthDays` for `MonthDayNumber`, `interval` for the
    /// `EveryXth…` types.
    /// </remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="request">The schedule.</param>
    /// <response code="200">The card with its repeat.</response>
    /// <response code="400">The schedule is incomplete, or the target column isn't on the card's board.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPut("{id:int}/repeat")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CardDetailDTO>> SetRepeat(int id, [FromBody] SetRepeatRequest request)
    {
        var selected = request.Type switch
        {
            RepeatType.WeekDays => string.Join(',', request.WeekDays ?? []),
            RepeatType.MonthDayNumber => string.Join(',', request.MonthDays ?? []),
            _ => null,
        };

        var result = await cardService.SetRepeatAsync(id, UserId, new RepeatInfo
        {
            Type = request.Type,
            Time = request.Time,
            LastRepeat = (request.StartDate ?? DateOnly.FromDateTime(DateTime.Now)).ToDateTime(request.Time),
            Selected = selected,
            Number = request.Interval,
            CardPlacement = request.Placement,
            TargetColumnId = request.TargetColumnId,
        });

        // The service names the stored fields; the request names them for people.
        if (result.Status == OperationStatus.Invalid)
            return Invalid(
                result.Field switch
                {
                    nameof(RepeatInfo.Selected) => request.Type == RepeatType.WeekDays ? nameof(request.WeekDays) : nameof(request.MonthDays),
                    nameof(RepeatInfo.Number) => nameof(request.Interval),
                    nameof(RepeatInfo.CardPlacement) => nameof(request.Placement),
                    nameof(RepeatInfo.LastRepeat) => nameof(request.StartDate),
                    var field => field ?? string.Empty,
                },
                result.Message!
            );

        return await ApplyAsync(id, [() => Task.FromResult(result)]);
    }

    /// <summary>Stop a card repeating</summary>
    /// <param name="id">The card's id.</param>
    /// <response code="200">The card.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpDelete("{id:int}/repeat")]
    [ApiWrite]
    public Task<ActionResult<CardDetailDTO>> RemoveRepeat(int id) =>
        ApplyAsync(id, [() => cardService.RemoveRepeatAsync(id, UserId)]);

    #endregion

    /// <summary>
    /// Makes the changes in order and stops at the first that fails. Then returns the card and tells open boards, or
    /// the failure.
    /// </summary>
    private async Task<ActionResult<CardDetailDTO>> ApplyAsync(int id, IEnumerable<Func<Task<OperationResult>>> changes)
    {
        foreach (var change in changes)
        {
            var result = await change();

            if (!result.Succeeded)
                return Failure(result);
        }

        var detail = await LoadDetailAsync(id);

        if (detail is null)
            return NotFound();

        await NotifyBoardsAsync(detail.BoardId);
        return detail;
    }

    private async Task<ActionResult<IEnumerable<CardAssigneeDTO>>> SetAssigneeAsync(int id, int userId, bool assigned)
    {
        var result = await cardService.SetAssigneeAsync(id, UserId, userId, assigned);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyCardBoardsAsync(id);
        return Ok(result.Value!.OrderBy(x => x.DisplayName).Select(x => x.ToAssigneeDto()));
    }

    private async Task<ActionResult<IEnumerable<LabelDTO>>> SetLabelAsync(int id, int labelId, bool added)
    {
        var result = await cardService.SetLabelAsync(id, UserId, labelId, added);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyCardBoardsAsync(id);
        return Ok(result.Value!.OrderBy(x => x.Id).Select(x => x.ToDto()));
    }

    /// <summary>The card as the API shows it, or null if it doesn't exist or the caller can't access it.</summary>
    private async Task<CardDetailDTO?> LoadDetailAsync(int id)
    {
        using var db = dbContextFactory.CreateDbContext();

        return await db.Cards
            .AccessibleBy(UserId)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => x.Id == id)
            .Select(ApiMappings.CardDetailProjection)
            .FirstOrDefaultAsync();
    }
}
