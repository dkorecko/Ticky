using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace Ticky.Internal.Helpers;

public static class BoardAccessHelper
{
    /// <summary>
    /// Whether the user is an admin of the board (see <see cref="Board.IsAdmin(User)"/>), read from the database rather than
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

    /// <summary>Only the boards the user may open (see <see cref="Board.AccessibleBy"/>).</summary>
    public static IQueryable<Board> AccessibleBy(this IQueryable<Board> boards, int userId) =>
        boards.Where(Board.AccessibleBy(userId));

    /// <summary>Only the columns on boards the user may open.</summary>
    public static IQueryable<Column> AccessibleBy(this IQueryable<Column> columns, int userId) =>
        columns.Where(OnBoard<Column>(column => column.Board, userId));

    /// <summary>Only the cards on boards the user may open.</summary>
    public static IQueryable<Card> AccessibleBy(this IQueryable<Card> cards, int userId) =>
        cards.Where(OnBoard<Card>(card => card.Column.Board, userId));

    /// <summary>
    /// <see cref="Board.AccessibleBy"/> applied to the board that <paramref name="board"/> leads to, so the rule is
    /// written once for boards, columns and cards.
    /// </summary>
    private static Expression<Func<T, bool>> OnBoard<T>(Expression<Func<T, Board>> board, int userId)
    {
        var rule = Board.AccessibleBy(userId);
        var body = ReplacingExpressionVisitor.Replace(rule.Parameters[0], board.Body, rule.Body);

        return Expression.Lambda<Func<T, bool>>(body, board.Parameters);
    }
}
