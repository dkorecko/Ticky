namespace Ticky.Web.Hubs;

[Authorize]
public class UpdateHub : Hub
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;

    public UpdateHub(IDbContextFactory<DataContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    private async Task<bool> IsUserInBoard(int boardId)
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
            return false;

        using var db = _dbContextFactory.CreateDbContext();

        var success = int.TryParse(
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            out int userId
        );

        if (!success)
            return false;

        return await db
            .Boards.Include(x => x.Memberships)
            .Include(x => x.Project)
            .ThenInclude(x => x.Memberships)
            .AnyAsync(b =>
                b.Id == boardId
                && (
                    b.Memberships.Any(m => m.UserId == userId)
                    || b.Project.Memberships.Any(pm => pm.UserId == userId)
                )
            );
    }

    public async Task BoardChange(int boardId, Guid pageId)
    {
        await Clients.Group($"board_{boardId}").SendAsync(nameof(BoardChange), boardId, pageId);
    }

    public async Task SubscribeBoard(int boardId)
    {
        if (!await IsUserInBoard(boardId))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"board_{boardId}");
    }
}
