namespace Ticky.Web.Hubs;

public class UpdateHub : Hub
{
    public async Task BoardChange(int boardId, Guid pageId)
    {
        await Clients.Group($"board_{boardId}").SendAsync(nameof(BoardChange), boardId, pageId);
    }

    public async Task SubscribeBoard(int boardId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"board_{boardId}");
    }
}
