using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers;

[Authorize(Policy = Constants.Policies.ApiCallerAuthenticated)]
[Route("api/v1/[controller]")]
[ApiController]
public class BoardsController : ControllerBase
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;

    public BoardsController(IDbContextFactory<DataContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>Boards the authenticated user has access to, via board or project membership.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BoardSummaryDTO>>> List()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        using var db = _dbContextFactory.CreateDbContext();

        var boards = await db.Boards
            .AsNoTracking()
            .Include(x => x.Project)
                .ThenInclude(p => p.Memberships)
            .Include(x => x.Memberships)
            .Where(x =>
                x.Memberships.Any(m => m.UserId == userId)
                || x.Project.Memberships.Any(m => m.UserId == userId)
            )
            .ToListAsync();

        return Ok(boards.Select(x => ToSummary(x, userId)));
    }

    /// <summary>One board with its columns and labels.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BoardDetailDTO>> Get(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        using var db = _dbContextFactory.CreateDbContext();

        var board = await db.Boards
            .AsNoTracking()
            .Include(x => x.Project)
                .ThenInclude(p => p.Memberships)
            .Include(x => x.Memberships)
            .Include(x => x.Columns.OrderBy(c => c.Index))
                .ThenInclude(c => c.Cards)
            .Include(x => x.Labels)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (board is null || !HasAccess(board, userId))
            return NotFound();

        var columns = board.Columns
            .OrderBy(c => c.Index)
            .Select(c => new ColumnDTO(c.Id, c.Name, c.Index, c.Finished, c.Cards.Count))
            .ToList();

        var labels = board.Labels
            .Select(l => new LabelDTO(l.Id, l.Name, FormatColor(l.BackgroundColor), FormatColor(l.TextColor)))
            .ToList();

        var summary = ToSummary(board, userId);

        return Ok(new BoardDetailDTO(
            summary.Id,
            summary.Name,
            summary.Description,
            summary.Code,
            summary.ProjectId,
            summary.ProjectName,
            summary.IsAdmin,
            columns,
            labels
        ));
    }

    /// <summary>All cards on a board, newest first within each column.</summary>
    [HttpGet("{id:int}/cards")]
    public async Task<ActionResult<IEnumerable<CardSummaryDTO>>> ListCards(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        using var db = _dbContextFactory.CreateDbContext();

        var board = await db.Boards
            .AsNoTracking()
            .Include(x => x.Project)
                .ThenInclude(p => p.Memberships)
            .Include(x => x.Memberships)
            .Include(x => x.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(card => card.Assignees)
            .Include(x => x.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(card => card.Labels)
            .Include(x => x.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(card => card.Subtasks)
            .Include(x => x.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(card => card.Attachments)
            .Include(x => x.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(card => card.Comments)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (board is null || !HasAccess(board, userId))
            return NotFound();

        var result = board.Columns
            .OrderBy(c => c.Index)
            .SelectMany(c => c.Cards
                .OrderBy(card => card.Index)
                .Select(card => new CardSummaryDTO(
                    card.Id,
                    card.Number,
                    $"{board.Code}-{card.Number}",
                    card.Name,
                    c.Id,
                    c.Name,
                    card.Index,
                    card.Priority,
                    card.Deadline,
                    card.Flagged,
                    card.Assignees.Select(a => a.Id).ToList(),
                    card.Labels.Select(l => l.Id).ToList(),
                    card.Subtasks.Count,
                    card.Subtasks.Count(s => s.Completed),
                    card.Attachments.Count,
                    card.Comments.Count
                )))
            .ToList();

        return Ok(result);
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId) && userId != 0;
    }

    private static bool HasAccess(Board board, int userId) =>
        board.Memberships.Any(m => m.UserId == userId)
        || board.Project.Memberships.Any(m => m.UserId == userId);

    private static BoardSummaryDTO ToSummary(Board board, int userId)
    {
        var boardMembership = board.Memberships.FirstOrDefault(m => m.UserId == userId);
        var projectMembership = board.Project.Memberships.FirstOrDefault(m => m.UserId == userId);
        var isAdmin = boardMembership is not null
            ? boardMembership.IsAdmin
            : projectMembership?.IsAdmin ?? false;

        return new BoardSummaryDTO(
            board.Id,
            board.Name,
            board.Description,
            board.Code,
            board.ProjectId,
            board.Project.Name,
            isAdmin
        );
    }

    private static string FormatColor(System.Drawing.Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
