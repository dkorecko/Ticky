using System.ComponentModel;
using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// Boards, their cards, members and activity. Every member can read a board and work on its cards; board admins
/// change the board, its columns, labels and members.
/// </summary>
[Route(Constants.Api.RoutePrefix + "/boards")]
[Tags("Boards")]
public class BoardsController(
    IDbContextFactory<DataContext> dbContextFactory,
    BoardService boardService,
    MembershipService membershipService
) : ApiControllerBase
{
    /// <summary>List boards</summary>
    /// <remarks>
    /// Every board you can open: those you're a member of, and all boards of projects you're a member of.
    /// `isAdmin` says whether you administer the board.
    /// </remarks>
    /// <response code="200">The boards.</response>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BoardSummaryDTO>>> List()
    {
        using var db = dbContextFactory.CreateDbContext();

        var boards = await db.Boards
            .AccessibleBy(UserId)
            .OrderBy(x => x.Project.Name)
            .ThenBy(x => x.Name)
            .WithAccessDetails(UserId)
            .ToListAsync();

        return Ok(boards.Select(board => board.ToSummaryDto(UserId)));
    }

    /// <summary>Get a board</summary>
    /// <remarks>
    /// One board with its columns (in board order, with how many cards each holds), labels and members. Use the
    /// column ids to create and move cards, the label ids to label them and the members' user ids to assign them.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <response code="200">The board.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpGet("{id:int}")]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoardDetailDTO>> Get(int id)
    {
        var board = await LoadDetailAsync(dbContextFactory, id, UserId);

        if (board is null)
            return NotFound();

        return board;
    }

    /// <summary>Update a board</summary>
    /// <remarks>
    /// Changes only the fields you send. Changing the code changes every card's key. Board admins only.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <param name="request">The fields to change.</param>
    /// <response code="200">The board after the changes.</response>
    /// <response code="400">The name is blank, or the code isn't 1 to 5 upper-case letters.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpPatch("{id:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoardDetailDTO>> Update(int id, [FromBody] UpdateBoardRequest request)
    {
        var result = await boardService.UpdateAsync(id, UserId, new()
        {
            Name = request.Name,
            Description = request.Description,
            Code = request.Code,
            DisableSortingAnimations = request.DisableSortingAnimations,
        });

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(id);
        return (await LoadDetailAsync(dbContextFactory, id, UserId))!;
    }

    /// <summary>Delete a board</summary>
    /// <remarks>Deletes the board with all its columns, cards and labels. This can't be undone. Board admins only.</remarks>
    /// <param name="id">The board's id.</param>
    /// <response code="204">The board is deleted.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpDelete("{id:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(int id)
    {
        var result = await boardService.DeleteAsync(id, UserId);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(id);
        return NoContent();
    }

    /// <summary>Clone a board</summary>
    /// <remarks>
    /// Copies the board into a project you administer, as "Copy of …": its board members, labels, columns and cards
    /// with their labels, assignees, subtasks, reminders and repeats. Comments, attachments, time records, links and
    /// activity aren't copied. Admins of the board only.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <param name="request">Where to put the copy, and its code.</param>
    /// <response code="201">The copy.</response>
    /// <response code="400">The code isn't 1 to 5 upper-case letters, or you don't administer the target project.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpPost("{id:int}/clone")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BoardDetailDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BoardDetailDTO>> Clone(int id, [FromBody] CloneBoardRequest request)
    {
        var result = await boardService.CloneAsync(id, UserId, request.TargetProjectId, request.Code);

        if (!result.Succeeded)
            return Failure(result);

        var copy = await LoadDetailAsync(dbContextFactory, result.Value!.Id, UserId);
        return CreatedAtAction(nameof(Get), new { id = copy!.Id }, copy);
    }

    /// <summary>Add a board to your favorites</summary>
    /// <remarks>Your favorites come first on the home page. Adding one that already is changes nothing.</remarks>
    /// <param name="id">The board's id.</param>
    /// <response code="204">The board is a favorite.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpPut("{id:int}/favorite")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<ActionResult> AddFavorite(int id) => SetFavoriteAsync(id, true);

    /// <summary>Remove a board from your favorites</summary>
    /// <param name="id">The board's id.</param>
    /// <response code="204">The board isn't a favorite.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpDelete("{id:int}/favorite")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<ActionResult> RemoveFavorite(int id) => SetFavoriteAsync(id, false);

    /// <summary>List a board's cards</summary>
    /// <remarks>
    /// The board's cards in board order: column by column, top to bottom, filtered like the board's filter if you
    /// ask. Each card is a short version with assignee and label ids and counts of subtasks, attachments and comments;
    /// get a card for everything else. Snoozed cards are included unless you leave them out; the board hides them.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <param name="query">Filters; every one is optional.</param>
    /// <response code="200">The cards.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpGet("{id:int}/cards")]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CardSummaryDTO>>> ListCards(int id, [FromQuery] BoardCardsQuery query)
    {
        using var db = dbContextFactory.CreateDbContext();

        if (!await db.Boards.AccessibleBy(UserId).AnyAsync(x => x.Id == id))
            return NotFound();

        var cards = db.Cards
            .Where(x => x.Column.BoardId == id)
            .Matching(new FilterCardsModel
            {
                Text = query.Q ?? string.Empty,
                AssignedUserIds = [.. query.AssigneeIds ?? []],
                IncludeUnassigned = query.Unassigned,
                LabelIds = [.. query.LabelIds ?? []],
                ExcludedLabelIds = [.. query.ExcludeLabelIds ?? []],
                ExcludeCompleted = query.ExcludeFinished,
            });

        if (query.ColumnId is { } columnId)
            cards = cards.Where(x => x.ColumnId == columnId);

        cards = query.Snoozed switch
        {
            SnoozedFilter.Exclude => cards.Where(x => x.SnoozedUntil == null),
            SnoozedFilter.Only => cards.Where(x => x.SnoozedUntil != null),
            _ => cards,
        };

        // Split: assignee and label ids are two collections per card, which one joined query would multiply.
        var result = await cards
            .AsSplitQuery()
            .OrderBy(x => x.Column.Index)
            .ThenBy(x => x.Index)
            .Select(ApiMappings.CardSummaryProjection)
            .ToListAsync();

        return Ok(result);
    }

    /// <summary>Get a board's activity</summary>
    /// <remarks>
    /// What happened on the board's cards, newest first, a page at a time: pass the response's `nextBefore` as
    /// `before` to get older entries.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <param name="before">Only entries older than this activity id, from the previous page's `nextBefore`.</param>
    /// <param name="limit">How many entries to return, at most 200.</param>
    /// <param name="type">Only entries of this kind, e.g. `CardMoved`.</param>
    /// <response code="200">A page of activity.</response>
    /// <response code="400">The limit isn't between 1 and 200, or `before` isn't an activity on this board.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpGet("{id:int}/activity")]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityPageDTO>> Activity(
        int id,
        [FromQuery] int? before,
        [FromQuery, DefaultValue(50)] int limit = 50,
        [FromQuery] ActivityType? type = null
    )
    {
        using var db = dbContextFactory.CreateDbContext();

        if (!await db.Boards.AccessibleBy(UserId).AnyAsync(x => x.Id == id))
            return NotFound();

        return await ActivityPageAsync(db.Activities.Where(x => x.Card.Column.BoardId == id), before, limit, type);
    }

    #region Members

    /// <summary>List a board's members</summary>
    /// <remarks>
    /// Everyone who can open the board, and so can be assigned to its cards: its own members, then its project's
    /// members. A board membership decides the role. Admins first.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <response code="200">The members.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpGet("{id:int}/members")]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<MemberDTO>>> ListMembers(int id)
    {
        var board = await LoadDetailAsync(dbContextFactory, id, UserId);

        if (board is null)
            return NotFound();

        return Ok(board.Members);
    }

    /// <summary>Add a board member</summary>
    /// <remarks>
    /// Gives the person with this e-mail address a membership of the board, as a member. Board admins only. Members of
    /// the board's project can open it already; a board membership lets you give them a different role here.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <param name="request">The person's e-mail address.</param>
    /// <response code="201">The new member.</response>
    /// <response code="400">There is no account with this e-mail address.</response>
    /// <response code="403">The token is read-only, or you aren't an admin of the board.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    /// <response code="409">They already have a membership of the board, or it's you.</response>
    [HttpPost("{id:int}/members")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status404NotFound), ProblemResponse(StatusCodes.Status409Conflict)]
    [ProducesResponseType<MemberDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MemberDTO>> AddMember(int id, [FromBody] AddMemberRequest request)
    {
        var result = await membershipService.AddBoardMemberAsync(id, UserId, request.Email);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(id);
        return CreatedAtAction(nameof(ListMembers), new { id }, result.Value!.ToDto(MembershipSource.Board));
    }

    /// <summary>Change a board member's role</summary>
    /// <remarks>
    /// Makes someone with a membership of the board an admin of it, or a member again. Project members without one
    /// get their role from the project. You can't change your own role.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <param name="userId">The member's user id.</param>
    /// <param name="request">The role.</param>
    /// <response code="200">The member.</response>
    /// <response code="403">The token is read-only, you aren't an admin of the board, or it's your own role.</response>
    /// <response code="404">The board doesn't exist, you can't open it, or they have no membership of it.</response>
    [HttpPatch("{id:int}/members/{userId:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MemberDTO>> UpdateMember(int id, int userId, [FromBody] UpdateMemberRequest request)
    {
        var result = await membershipService.SetBoardAdminAsync(id, UserId, userId, request.IsAdmin);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(id);
        var board = await LoadDetailAsync(dbContextFactory, id, UserId);
        return board!.Members.First(x => x.UserId == userId);
    }

    /// <summary>Remove a board member</summary>
    /// <remarks>
    /// Takes away their membership of the board. Members of its project keep their access through the project. You
    /// can't remove yourself.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <param name="userId">The member's user id.</param>
    /// <response code="204">The membership is gone.</response>
    /// <response code="403">The token is read-only, you aren't an admin of the board, or it's yourself.</response>
    /// <response code="404">The board doesn't exist, you can't open it, or they have no membership of it.</response>
    [HttpDelete("{id:int}/members/{userId:int}")]
    [ApiWrite]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RemoveMember(int id, int userId)
    {
        var result = await membershipService.RemoveBoardMemberAsync(id, UserId, userId);

        if (!result.Succeeded)
            return Failure(result);

        await NotifyBoardsAsync(id);
        return NoContent();
    }

    #endregion

    /// <summary>The board as the API shows it, or null if it doesn't exist or the user can't open it.</summary>
    internal static async Task<BoardDetailDTO?> LoadDetailAsync(IDbContextFactory<DataContext> dbContextFactory, int id, int userId)
    {
        using var db = dbContextFactory.CreateDbContext();

        var board = await db.Boards
            .AccessibleBy(userId)
            .WithAccessDetails(userId)
            .Include(x => x.Labels)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (board is null)
            return null;

        var columns = await db.Columns
            .Where(x => x.BoardId == id)
            .OrderBy(x => x.Index)
            .Select(ApiMappings.ColumnProjection)
            .ToListAsync();

        return board.ToDetailDto(userId, columns);
    }

    private async Task<ActionResult> SetFavoriteAsync(int id, bool favorite)
    {
        var result = await boardService.SetFavoriteAsync(id, UserId, favorite);

        return result.Succeeded ? NoContent() : Failure(result);
    }
}
