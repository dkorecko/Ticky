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
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) || userId == 0)
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

        var result = boards.Select(x =>
        {
            var boardMembership = x.Memberships.FirstOrDefault(m => m.UserId == userId);
            var projectMembership = x.Project.Memberships.FirstOrDefault(m => m.UserId == userId);
            var isAdmin = boardMembership is not null
                ? boardMembership.IsAdmin
                : projectMembership?.IsAdmin ?? false;

            return new BoardSummaryDTO(
                x.Id,
                x.Name,
                x.Description,
                x.Code,
                x.ProjectId,
                x.Project.Name,
                isAdmin
            );
        });

        return Ok(result);
    }
}
