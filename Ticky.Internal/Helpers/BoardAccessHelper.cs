namespace Ticky.Internal.Helpers;

public static class BoardAccessHelper
{
    /// <summary>
    /// Whether the user is an admin of the board (see <see cref="Board.IsAdmin"/>), read from the database rather than
    /// from a board loaded earlier, since the role may have changed since then. False if the board does not exist.
    /// </summary>
    public static async Task<bool> IsBoardAdminAsync(this DataContext db, int boardId, User user)
    {
        var board = await db.Boards
            .Include(x => x.Memberships)
            .Include(x => x.Project)
                .ThenInclude(x => x.Memberships)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId);

        return board is not null && board.IsAdmin(user);
    }
}
