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

    /// <summary>
    /// Whether the user is an admin of the project (see <see cref="Project.AdministeredBy"/>), read from the database
    /// rather than from a project loaded earlier, since the role may have changed since then. False if the project does
    /// not exist.
    /// </summary>
    public static Task<bool> IsProjectAdminAsync(this DataContext db, int projectId, int userId) =>
        db.Projects.Where(x => x.Id == projectId).AnyAsync(Project.AdministeredBy(userId));

    /// <summary>Only the boards the user may open (see <see cref="Board.AccessibleBy"/>).</summary>
    public static IQueryable<Board> AccessibleBy(this IQueryable<Board> boards, int userId) =>
        boards.Where(Board.AccessibleBy(userId));

    /// <summary>Only the columns on boards the user may open.</summary>
    public static IQueryable<Column> AccessibleBy(this IQueryable<Column> columns, int userId) =>
        columns.Where(OnBoard<Column>(column => column.Board, userId));

    /// <summary>Only the cards on boards the user may open.</summary>
    public static IQueryable<Card> AccessibleBy(this IQueryable<Card> cards, int userId) =>
        cards.Where(OnBoard<Card>(card => card.Column.Board, userId));

    /// <summary>Only the subtasks of cards on boards the user may open.</summary>
    public static IQueryable<Subtask> AccessibleBy(this IQueryable<Subtask> subtasks, int userId) =>
        subtasks.Where(OnBoard<Subtask>(subtask => subtask.Card.Column.Board, userId));

    /// <summary>Only the time records of cards on boards the user may open.</summary>
    public static IQueryable<TimeRecord> AccessibleBy(this IQueryable<TimeRecord> timeRecords, int userId) =>
        timeRecords.Where(OnBoard<TimeRecord>(record => record.Card.Column.Board, userId));

    /// <summary>Only the reminders of cards on boards the user may open.</summary>
    public static IQueryable<Reminder> AccessibleBy(this IQueryable<Reminder> reminders, int userId) =>
        reminders.Where(OnBoard<Reminder>(reminder => reminder.Card.Column.Board, userId));

    /// <summary>Only the attachments of cards on boards the user may open.</summary>
    public static IQueryable<Attachment> AccessibleBy(this IQueryable<Attachment> attachments, int userId) =>
        attachments.Where(OnBoard<Attachment>(attachment => attachment.Card.Column.Board, userId));

    /// <summary>
    /// Only the links whose first card is on a board the user may open. A link is stored once per direction, so this
    /// is the card the user sees the link on.
    /// </summary>
    public static IQueryable<CardLink> AccessibleBy(this IQueryable<CardLink> links, int userId) =>
        links.Where(OnBoard<CardLink>(link => link.CardOne.Column.Board, userId));

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
