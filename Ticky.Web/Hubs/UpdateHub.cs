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

        return await db.Boards.AccessibleBy(userId).AnyAsync(b => b.Id == boardId);
    }

    /// <summary>The group of connections viewing a board, which <see cref="BoardChange"/> is sent to.</summary>
    public static string BoardGroup(int boardId) => $"board_{boardId}";

    public async Task BoardChange(int boardId, Guid pageId)
    {
        await Clients.Group(BoardGroup(boardId)).SendAsync(nameof(BoardChange), boardId, pageId);
    }

    public async Task SubscribeBoard(int boardId)
    {
        if (!await IsUserInBoard(boardId))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup(boardId));
    }
}
