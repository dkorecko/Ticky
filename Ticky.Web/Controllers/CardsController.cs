using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers;

[Authorize(Policy = Constants.Policies.ApiCallerAuthenticated)]
[Route("api/v1/[controller]")]
[ApiController]
public class CardsController : ControllerBase
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;
    private readonly CardService _cardService;

    public CardsController(IDbContextFactory<DataContext> dbContextFactory, CardService cardService)
    {
        _dbContextFactory = dbContextFactory;
        _cardService = cardService;
    }

    /// <summary>Full detail for one card, including description, subtasks, comments and assignees.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CardDetailDTO>> Get(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        // Same-response-shape-as-missing when access is denied so the endpoint doesn't leak which ids exist.
        if (!await _cardService.CanAccessCardAsync(id, userId))
            return NotFound();

        var detail = await LoadDetailAsync(id);

        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost]
    public async Task<ActionResult<CardDetailDTO>> Create([FromBody] CreateCardRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var card = await _cardService.CreateCardAsync(request.ColumnId, userId, request.Name.Trim());

        if (card is null)
            return NotFound(new { error = "Column not found or access denied." });

        return CreatedAtAction(nameof(Get), new { id = card.Id }, await LoadDetailAsync(card.Id));
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<CardDetailDTO>> Update(int id, [FromBody] UpdateCardRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!await _cardService.CanAccessCardAsync(id, userId))
            return NotFound();

        if (request.Name is not null)
            await _cardService.RenameAsync(id, userId, request.Name.Trim());

        if (request.Description is not null)
            await _cardService.UpdateDescriptionAsync(id, userId, request.Description);

        if (request.Priority.HasValue)
            await _cardService.SetPriorityAsync(id, userId, request.Priority.Value);

        if (request.ClearDeadline == true)
            await _cardService.RemoveDeadlineAsync(id, userId);
        else if (request.Deadline.HasValue)
            await _cardService.SetDeadlineAsync(id, userId, request.Deadline.Value);

        if (request.Flagged.HasValue)
            await SetFlaggedAsync(id, request.Flagged.Value);

        return Ok(await LoadDetailAsync(id));
    }

    [HttpPost("{id:int}/move")]
    public async Task<ActionResult<CardDetailDTO>> Move(int id, [FromBody] MoveCardRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!await _cardService.CanAccessCardAsync(id, userId))
            return NotFound();

        var moved = await _cardService.MoveToColumnAsync(id, userId, request.TargetColumnId, request.BeforeCardId);

        if (!moved)
            return Conflict(new { error = "Target column is full or lives on a different board." });

        return Ok(await LoadDetailAsync(id));
    }

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<CardCommentDTO>> AddComment(int id, [FromBody] AddCommentRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!await _cardService.CanAccessCardAsync(id, userId))
            return NotFound();

        await _cardService.AddCommentAsync(id, userId, request.Text.Trim());

        using var db = _dbContextFactory.CreateDbContext();
        var latest = await db.Comments
            .AsNoTracking()
            .Include(c => c.CreatedBy)
            .Where(c => c.CardId == id && c.CreatedById == userId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstAsync();

        return CreatedAtAction(nameof(Get), new { id = id }, new CardCommentDTO(
            latest.Id,
            latest.Text,
            latest.CreatedAt,
            latest.CreatedById,
            latest.CreatedBy.DisplayName
        ));
    }

    private async Task SetFlaggedAsync(int cardId, bool flagged)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var card = await db.Cards.FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null || card.Flagged == flagged)
            return;
        card.Flagged = flagged;
        await db.SaveChangesAsync();
    }

    private async Task<CardDetailDTO?> LoadDetailAsync(int id)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var card = await db.Cards
            .AsNoTracking()
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .Include(c => c.Assignees)
            .Include(c => c.Labels)
            .Include(c => c.Subtasks)
            .Include(c => c.Comments)
                .ThenInclude(cm => cm.CreatedBy)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card is null)
            return null;

        return new CardDetailDTO(
            card.Id,
            card.Number,
            $"{card.Column.Board.Code}-{card.Number}",
            card.Name,
            card.Description,
            card.ColumnId,
            card.Column.Name,
            card.Column.BoardId,
            card.Index,
            card.Priority,
            card.Deadline,
            card.Flagged,
            card.SnoozedUntil,
            card.CreatedAt,
            card.CreatedById,
            card.Assignees.Select(a => new CardAssigneeDTO(a.Id, a.DisplayName)).ToList(),
            card.Labels.Select(l => new LabelDTO(l.Id, l.Name, FormatColor(l.BackgroundColor), FormatColor(l.TextColor))).ToList(),
            card.Subtasks.OrderBy(s => s.Index).Select(s => new CardSubtaskDTO(s.Id, s.Text, s.Completed, s.Index)).ToList(),
            card.Comments.OrderByDescending(c => c.CreatedAt).Select(c => new CardCommentDTO(
                c.Id, c.Text, c.CreatedAt, c.CreatedById, c.CreatedBy.DisplayName
            )).ToList()
        );
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId) && userId != 0;
    }

    private static string FormatColor(System.Drawing.Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
