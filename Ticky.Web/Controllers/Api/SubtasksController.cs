using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>A card's subtasks: a checklist whose items can be assigned and reordered. A card lists them in order.</summary>
[Route(Constants.Api.RoutePrefix + "/cards/{cardId:int}/subtasks")]
[Tags("Subtasks")]
[ApiWrite]
[ProblemResponse(StatusCodes.Status404NotFound)]
public class SubtasksController(IDbContextFactory<DataContext> dbContextFactory, CardService cardService) : ApiControllerBase
{
    /// <summary>Add a subtask</summary>
    /// <remarks>Adds an item at the end of the card's checklist.</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="request">The subtask.</param>
    /// <response code="201">The new subtask.</response>
    /// <response code="400">The text is missing or blank.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPost]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<SubtaskDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SubtaskDTO>> Create(int cardId, [FromBody] AddSubtaskRequest request)
    {
        var result = await cardService.AddSubtaskAsync(cardId, UserId, request.Text);

        if (await FailureOrNotifyAsync(result, cardId) is { } refused)
            return refused;

        return CreatedAtAction(nameof(CardsController.Get), "Cards", new { id = cardId }, await LoadAsync(result.Value!.Id));
    }

    /// <summary>Update a subtask</summary>
    /// <remarks>Changes its text, checks it off or opens it again. Only the fields you send change.</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="subtaskId">The subtask's id.</param>
    /// <param name="request">The fields to change.</param>
    /// <response code="200">The subtask after the changes.</response>
    /// <response code="400">The text is blank.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card or subtask doesn't exist, or you aren't a member of the card's board or project.</response>
    [HttpPatch("{subtaskId:int}")]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubtaskDTO>> Update(int cardId, int subtaskId, [FromBody] UpdateSubtaskRequest request)
    {
        if (request.Text is not null && await cardService.EditSubtaskAsync(cardId, UserId, subtaskId, request.Text) is { Succeeded: false } edit)
            return Failure(edit);

        if (request.Completed is { } completed && await cardService.SetSubtaskCompletedAsync(cardId, UserId, subtaskId, completed) is { Succeeded: false } check)
            return Failure(check);

        // Nothing sent still has to find the subtask.
        var subtask = await LoadAsync(subtaskId, cardId);

        if (subtask is null || !await cardService.CanAccessCardAsync(cardId, UserId))
            return NotFound();

        await NotifyCardBoardsAsync(cardId);
        return subtask;
    }

    /// <summary>Move a subtask</summary>
    /// <remarks>Puts the subtask before another of the card's subtasks, or last.</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="subtaskId">The subtask's id.</param>
    /// <param name="request">Where to put it.</param>
    /// <response code="200">The card's subtasks in their new order.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card or subtask doesn't exist, or you aren't a member of the card's board or project.</response>
    [HttpPost("{subtaskId:int}/move")]
    public async Task<ActionResult<IEnumerable<SubtaskDTO>>> Move(int cardId, int subtaskId, [FromBody] MoveSubtaskRequest request)
    {
        var result = await cardService.MoveSubtaskAsync(cardId, UserId, subtaskId, request.BeforeSubtaskId);

        if (await FailureOrNotifyAsync(result, cardId) is { } refused)
            return refused;

        using var db = dbContextFactory.CreateDbContext();
        var subtasks = await db.Subtasks
            .Where(x => x.CardId == cardId)
            .Include(x => x.Assignees)
            .OrderBy(x => x.Index)
            .AsNoTracking()
            .ToListAsync();

        return Ok(subtasks.Select(x => x.ToDto()));
    }

    /// <summary>Delete a subtask</summary>
    /// <param name="cardId">The card's id.</param>
    /// <param name="subtaskId">The subtask's id.</param>
    /// <response code="204">The subtask is deleted.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card or subtask doesn't exist, or you aren't a member of the card's board or project.</response>
    [HttpDelete("{subtaskId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(int cardId, int subtaskId)
    {
        return await NoContentOrFailureAsync(await cardService.DeleteSubtaskAsync(cardId, UserId, subtaskId), cardId);
    }

    /// <summary>Assign someone to a subtask</summary>
    /// <remarks>Assigns a member of the board to the subtask. Assigning someone who already is changes nothing.</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="subtaskId">The subtask's id.</param>
    /// <param name="userId">The member's user id.</param>
    /// <response code="200">The subtask.</response>
    /// <response code="400">They aren't a member of the board or its project.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card or subtask doesn't exist, or you aren't a member of the card's board or project.</response>
    [HttpPut("{subtaskId:int}/assignees/{userId:int}")]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<SubtaskDTO>> Assign(int cardId, int subtaskId, int userId) =>
        SetAssigneeAsync(cardId, subtaskId, userId, true);

    /// <summary>Unassign someone from a subtask</summary>
    /// <param name="cardId">The card's id.</param>
    /// <param name="subtaskId">The subtask's id.</param>
    /// <param name="userId">The assignee's user id.</param>
    /// <response code="200">The subtask.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card or subtask doesn't exist, or you aren't a member of the card's board or project.</response>
    [HttpDelete("{subtaskId:int}/assignees/{userId:int}")]
    public Task<ActionResult<SubtaskDTO>> Unassign(int cardId, int subtaskId, int userId) =>
        SetAssigneeAsync(cardId, subtaskId, userId, false);

    private async Task<ActionResult<SubtaskDTO>> SetAssigneeAsync(int cardId, int subtaskId, int userId, bool assigned)
    {
        var result = await cardService.SetSubtaskAssigneeAsync(cardId, UserId, subtaskId, userId, assigned);

        if (await FailureOrNotifyAsync(result, cardId) is { } refused)
            return refused;

        return (await LoadAsync(subtaskId))!;
    }

    private async Task<SubtaskDTO?> LoadAsync(int subtaskId, int? cardId = null)
    {
        using var db = dbContextFactory.CreateDbContext();
        var subtask = await db.Subtasks
            .Include(x => x.Assignees)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == subtaskId && (cardId == null || x.CardId == cardId));

        return subtask?.ToDto();
    }
}
