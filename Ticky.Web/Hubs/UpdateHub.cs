using System.Collections.Concurrent;

namespace Ticky.Web.Hubs;

/// <summary>
/// Tells pages and API clients viewing a board that it changed, so they load it again. Open to signed-in browsers and
/// to API tokens, which connect like any SignalR client.
/// </summary>
[Authorize(Policy = Constants.Policies.ApiCallerAuthenticated)]
public class UpdateHub : Hub
{
    /// <summary>Key in <see cref="HubCallerContext.Items"/> for the boards this connection has been verified for.</summary>
    private const string VerifiedBoardsKey = "VerifiedBoards";

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

    /// <summary>The boards this connection already passed the access check for; lives as long as the connection.</summary>
    private ConcurrentDictionary<int, byte> VerifiedBoards =>
        (ConcurrentDictionary<int, byte>)(
            Context.Items.TryGetValue(VerifiedBoardsKey, out var boards) && boards is not null
                ? boards
                : Context.Items[VerifiedBoardsKey] = new ConcurrentDictionary<int, byte>()
        );

    /// <summary>The group of connections viewing a board, which <see cref="BoardChange"/> is sent to.</summary>
    public static string BoardGroup(int boardId) => $"board_{boardId}";

    /// <summary>
    /// Tells the other viewers of a board to reload it. Only for boards the caller can open: the check runs once per
    /// board and connection (<see cref="SubscribeBoard"/> already did it for the boards the caller is viewing), so a
    /// change doesn't cost a query. A client that reconnected and didn't subscribe again is checked on its first change.
    /// Only for the web UI: API changes are announced by the server, and a read-only token mustn't make every viewer
    /// reload. Only token callers carry the scope claim.
    /// </summary>
    public async Task BoardChange(int boardId, Guid pageId)
    {
        if (Context.User?.HasClaim(x => x.Type == Constants.Api.ScopeClaim) == true)
            return;

        if (!VerifiedBoards.ContainsKey(boardId))
        {
            if (!await IsUserInBoard(boardId))
                return;

            VerifiedBoards[boardId] = 0;
        }

        await Clients.Group(BoardGroup(boardId)).SendAsync(nameof(BoardChange), boardId, pageId);
    }

    public async Task SubscribeBoard(int boardId)
    {
        if (!await IsUserInBoard(boardId))
            return;

        VerifiedBoards[boardId] = 0;
        await Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup(boardId));
    }
}
