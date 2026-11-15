using System.Net.Mime;
using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>Read and change cards.</summary>
[Route(Constants.Api.RoutePrefix + "/cards")]
public class CardsController : ApiControllerBase
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;
    private readonly CardService _cardService;
    private readonly IHubContext<UpdateHub> _updateHub;

    public CardsController(
        IDbContextFactory<DataContext> dbContextFactory,
        CardService cardService,
        IHubContext<UpdateHub> updateHub
    )
    {
        _dbContextFactory = dbContextFactory;
        _cardService = cardService;
        _updateHub = updateHub;
    }

    /// <summary>Get a card</summary>
    /// <remarks>Everything about one card: description, assignees, labels, subtasks and comments (newest first).</remarks>
    /// <param name="id">The card's id, as returned by the board's card list.</param>
    /// <response code="200">The card.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<CardDetailDTO>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<CardDetailDTO>> Get(int id)
    {
        // A card the caller can't access gets the same 404 as a missing one, so ids of other boards' cards don't leak.
        var detail = await LoadDetailAsync(id);

        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Create a card</summary>
    /// <remarks>
    /// Adds a card to the column, at the top or bottom as the column's settings say. Open boards update right away.
    /// </remarks>
    /// <response code="201">The new card.</response>
    /// <response code="400">The name is missing, blank or longer than 300 characters.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The column doesn't exist, or you aren't a member of its board or project.</response>
    /// <response code="409">The column already holds its maximum number of cards.</response>
    [HttpPost]
    [Authorize(Policy = Constants.Policies.ApiCallerCanWrite)]
    [ProducesResponseType<CardDetailDTO>(StatusCodes.Status201Created, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<CardDetailDTO>> Create([FromBody] CreateCardRequest request)
    {
        var result = await _cardService.CreateCardAsync(request.ColumnId, UserId, request.Name);

        switch (result.Status)
        {
            case CardService.CreateCardStatus.NotFound:
                return Problem(detail: "The column doesn't exist, or you can't access it.", statusCode: StatusCodes.Status404NotFound);
            case CardService.CreateCardStatus.ColumnFull:
                return Problem(detail: "The column is full.", statusCode: StatusCodes.Status409Conflict);
            case CardService.CreateCardStatus.InvalidName:
                ModelState.AddModelError(nameof(request.Name), "The name can't be empty.");
                return ValidationProblem(ModelState);
        }

        var detail = await LoadDetailAsync(result.Card!.Id);
        await NotifyBoardAsync(detail!.BoardId);

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
    [Authorize(Policy = Constants.Policies.ApiCallerCanWrite)]
    [ProducesResponseType<CardDetailDTO>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<CardDetailDTO>> Update(int id, [FromBody] UpdateCardRequest request)
    {
        if (!await _cardService.CanAccessCardAsync(id, UserId))
            return NotFound();

        var name = request.Name?.Trim();

        if (name is { Length: 0 })
        {
            ModelState.AddModelError(nameof(request.Name), "The name can't be empty.");
            return ValidationProblem(ModelState);
        }

        // Each of these does nothing when the value doesn't change, so the activity log only shows real changes.
        if (name is not null)
            await _cardService.RenameAsync(id, UserId, name);

        if (request.Description is not null)
            await _cardService.UpdateDescriptionAsync(id, UserId, request.Description);

        if (request.Priority.HasValue)
            await _cardService.SetPriorityAsync(id, UserId, request.Priority.Value);

        if (request.ClearDeadline == true)
            await _cardService.RemoveDeadlineAsync(id, UserId);
        else if (request.Deadline is DateTime deadline)
            await _cardService.SetDeadlineAsync(id, UserId, ToServerTime(deadline));

        if (request.Flagged.HasValue)
            await _cardService.SetFlaggedAsync(id, request.Flagged.Value);

        var detail = await LoadDetailAsync(id);
        await NotifyBoardAsync(detail!.BoardId);

        return Ok(detail);
    }

    /// <summary>Move a card</summary>
    /// <remarks>
    /// Moves the card to another column on the same board, before `beforeCardId` or at the end. Moving it to the
    /// column it's already in changes nothing.
    /// </remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="request">Where to move it.</param>
    /// <response code="200">The card in its new column.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    /// <response code="409">The target column is full, doesn't exist or is on another board.</response>
    [HttpPost("{id:int}/move")]
    [Authorize(Policy = Constants.Policies.ApiCallerCanWrite)]
    [ProducesResponseType<CardDetailDTO>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<CardDetailDTO>> Move(int id, [FromBody] MoveCardRequest request)
    {
        if (!await _cardService.CanAccessCardAsync(id, UserId))
            return NotFound();

        if (!await _cardService.MoveToColumnAsync(id, UserId, request.TargetColumnId, request.BeforeCardId))
            return Problem(
                detail: "The target column is full, doesn't exist or is on another board.",
                statusCode: StatusCodes.Status409Conflict
            );

        var detail = await LoadDetailAsync(id);
        await NotifyBoardAsync(detail!.BoardId);

        return Ok(detail);
    }

    /// <summary>Comment on a card</summary>
    /// <remarks>Adds a comment as you. Open boards update right away.</remarks>
    /// <param name="id">The card's id.</param>
    /// <param name="request">The comment.</param>
    /// <response code="201">The new comment.</response>
    /// <response code="400">The text is missing, blank or longer than 10,000 characters.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPost("{id:int}/comments")]
    [Authorize(Policy = Constants.Policies.ApiCallerCanWrite)]
    [ProducesResponseType<CardCommentDTO>(StatusCodes.Status201Created, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<CardCommentDTO>> AddComment(int id, [FromBody] AddCommentRequest request)
    {
        if (!await _cardService.CanAccessCardAsync(id, UserId))
            return NotFound();

        var comment = await _cardService.AddCommentAsync(id, UserId, request.Text.Trim());

        if (comment is null)
            return NotFound();

        using var db = _dbContextFactory.CreateDbContext();

        var created = await db.Comments
            .Where(x => x.Id == comment.Id)
            .Select(ApiMappings.CommentProjection)
            .FirstAsync();

        var boardId = await db.Cards.Where(x => x.Id == id).Select(x => x.Column.BoardId).FirstAsync();
        await NotifyBoardAsync(boardId);

        return CreatedAtAction(nameof(Get), new { id }, created);
    }

    /// <summary>
    /// Makes open boards reload, like the browser does after its own changes. Guid.Empty is no page's id,
    /// so every viewer reloads.
    /// </summary>
    private Task NotifyBoardAsync(int boardId) =>
        _updateHub.Clients
            .Group(UpdateHub.BoardGroup(boardId))
            .SendAsync(nameof(UpdateHub.BoardChange), boardId, Guid.Empty);

    /// <summary>Ticky stores server-local times; a time sent as UTC ("Z") is converted to it.</summary>
    private static DateTime ToServerTime(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;

    /// <summary>The card as the API shows it, or null if it doesn't exist or the caller can't access it.</summary>
    private async Task<CardDetailDTO?> LoadDetailAsync(int id)
    {
        using var db = _dbContextFactory.CreateDbContext();

        return await db.Cards
            .AccessibleBy(UserId)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => x.Id == id)
            .Select(ApiMappings.CardDetailProjection)
            .FirstOrDefaultAsync();
    }
}
