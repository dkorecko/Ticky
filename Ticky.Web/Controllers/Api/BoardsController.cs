using System.Net.Mime;
using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>Read boards and their cards.</summary>
[Route(Constants.Api.RoutePrefix + "/boards")]
public class BoardsController : ApiControllerBase
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;

    public BoardsController(IDbContextFactory<DataContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>List boards</summary>
    /// <remarks>
    /// Every board you can open: those you're a member of, and all boards of projects you're a member of.
    /// `isAdmin` says whether you administer the board.
    /// </remarks>
    /// <response code="200">The boards.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<BoardSummaryDTO>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public async Task<ActionResult<IEnumerable<BoardSummaryDTO>>> List()
    {
        using var db = _dbContextFactory.CreateDbContext();

        var boards = await AccessibleBoards(db).ToListAsync();

        return Ok(boards.Select(board => board.ToSummaryDto(UserId)));
    }

    /// <summary>Get a board</summary>
    /// <remarks>
    /// One board with its columns, in board order with how many cards each holds, and its labels. Use the column
    /// ids to create and move cards, and the label ids to read a card's labels.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <response code="200">The board.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<BoardDetailDTO>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<BoardDetailDTO>> Get(int id)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var board = await AccessibleBoards(db)
            .Include(x => x.Labels)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (board is null)
            return NotFound();

        var columns = await db.Columns
            .Where(x => x.BoardId == id)
            .OrderBy(x => x.Index)
            .Select(ApiMappings.ColumnProjection)
            .ToListAsync();

        return Ok(board.ToDetailDto(UserId, columns));
    }

    /// <summary>List a board's cards</summary>
    /// <remarks>
    /// Every card on the board in board order: column by column, top to bottom. Each card is a short version with
    /// assignee and label ids and counts of subtasks, attachments and comments; get a card for everything else.
    /// </remarks>
    /// <param name="id">The board's id.</param>
    /// <response code="200">The cards.</response>
    /// <response code="404">The board doesn't exist, or you aren't a member of it or its project.</response>
    [HttpGet("{id:int}/cards")]
    [ProducesResponseType<IEnumerable<CardSummaryDTO>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<IEnumerable<CardSummaryDTO>>> ListCards(int id)
    {
        using var db = _dbContextFactory.CreateDbContext();

        if (!await db.Boards.AccessibleBy(UserId).AnyAsync(x => x.Id == id))
            return NotFound();

        // Split: assignee and label ids are two collections per card, which one joined query would multiply.
        var cards = await db.Cards
            .AsSplitQuery()
            .Where(x => x.Column.BoardId == id)
            .OrderBy(x => x.Column.Index)
            .ThenBy(x => x.Index)
            .Select(ApiMappings.CardSummaryProjection)
            .ToListAsync();

        return Ok(cards);
    }

    /// <summary>The boards the caller can open, with the memberships <see cref="Board.IsAdmin(int)"/> needs.</summary>
    private IQueryable<Board> AccessibleBoards(DataContext db) =>
        db.Boards
            .AccessibleBy(UserId)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Memberships)
            .Include(x => x.Project)
                .ThenInclude(x => x.Memberships);
}
