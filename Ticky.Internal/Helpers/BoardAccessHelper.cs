using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace Ticky.Internal.Helpers;

/// <summary>What a user may do with a board or project.</summary>
public enum AccessLevel
{
    /// <summary>They can't open it, or it doesn't exist.</summary>
    None,

    Member,

    Admin
}

public static class BoardAccessHelper
{
    public const string BOARD_NOT_FOUND = "The board doesn't exist, or you can't access it.";
    public const string PROJECT_NOT_FOUND = "The project doesn't exist, or you aren't a member of it.";

    /// <summary>
    /// Whether the user is an admin of the board (see <see cref="Board.IsAdmin(User)"/>), read from the database rather than
    /// from a board loaded earlier, since the role may have changed since then. False if the board does not exist.
    /// </summary>
    public static async Task<bool> IsBoardAdminAsync(this DataContext db, int boardId, User user) =>
        await db.GetBoardAccessAsync(boardId, user.Id) == AccessLevel.Admin;

    /// <summary>
    /// What the user may do with the board, from <see cref="Board.VerifyAccess"/> and <see cref="Board.IsAdmin(int)"/>.
    /// Read from the database, since the role may have changed since a page loaded the board.
    /// </summary>
    public static async Task<AccessLevel> GetBoardAccessAsync(this DataContext db, int boardId, int userId)
    {
        // Only the user's own memberships are needed to apply both rules.
        var board = await db.Boards
            .AsNoTracking()
            .Include(x => x.Memberships.Where(m => m.UserId == userId))
            .Include(x => x.Project)
                .ThenInclude(x => x.Memberships.Where(m => m.UserId == userId))
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null || !board.VerifyAccess(userId))
            return AccessLevel.None;

        return board.IsAdmin(userId) ? AccessLevel.Admin : AccessLevel.Member;
    }

    /// <summary>
    /// Whether the user is an admin of the project (see <see cref="Project.AdministeredBy"/>), read from the database
    /// rather than from a project loaded earlier, since the role may have changed since then. False if the project does
    /// not exist.
    /// </summary>
    public static async Task<bool> IsProjectAdminAsync(this DataContext db, int projectId, int userId) =>
        await db.GetProjectAccessAsync(projectId, userId) == AccessLevel.Admin;

    /// <summary>What the user may do with the project: its members can open it, and its admins manage it.</summary>
    public static async Task<AccessLevel> GetProjectAccessAsync(this DataContext db, int projectId, int userId)
    {
        var isAdmin = await db.ProjectMemberships
            .Where(x => x.ProjectId == projectId && x.UserId == userId)
            .Select(x => (bool?)x.IsAdmin)
            .FirstOrDefaultAsync();

        return isAdmin switch
        {
            null => AccessLevel.None,
            true => AccessLevel.Admin,
            false => AccessLevel.Member,
        };
    }

    /// <summary>
    /// Null if the user administers the board; otherwise why they can't make the change, e.g.
    /// "Only board admins can manage labels." for <paramref name="action"/> "manage labels".
    /// </summary>
    public static async Task<OperationResult?> CheckBoardAdminAsync(this DataContext db, int boardId, int userId, string action) =>
        await db.GetBoardAccessAsync(boardId, userId) switch
        {
            AccessLevel.None => OperationResult.NotFound(BOARD_NOT_FOUND),
            AccessLevel.Member => OperationResult.Forbidden($"Only board admins can {action}."),
            _ => null,
        };

    /// <summary>
    /// Like <see cref="CheckBoardAdminAsync(DataContext, int, int, string)"/>, for something on a board, like a column
    /// or label, given its board's id, or null if it doesn't exist. Missing, or on a board the user can't open, is
    /// <paramref name="notFound"/>.
    /// </summary>
    public static async Task<OperationResult?> CheckBoardAdminAsync(this DataContext db, int? boardId, int userId, string action, string notFound)
    {
        var denied = boardId is { } id ? await db.CheckBoardAdminAsync(id, userId, action) : OperationResult.NotFound(notFound);

        return denied?.Status == OperationStatus.NotFound ? OperationResult.NotFound(notFound) : denied;
    }

    /// <summary>Like <see cref="CheckBoardAdminAsync(DataContext, int, int, string)"/>, for the admins of a project.</summary>
    public static async Task<OperationResult?> CheckProjectAdminAsync(this DataContext db, int projectId, int userId, string action) =>
        await db.GetProjectAccessAsync(projectId, userId) switch
        {
            AccessLevel.None => OperationResult.NotFound(PROJECT_NOT_FOUND),
            AccessLevel.Member => OperationResult.Forbidden($"Only project admins can {action}."),
            _ => null,
        };

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
    /// The people who can open the board, and so can be assigned to its cards: its members and its project's members.
    /// The same rule as <see cref="Board.AccessibleBy"/>, seen from the user's side.
    /// </summary>
    public static IQueryable<User> MembersOf(this IQueryable<User> users, int boardId) =>
        users.Where(user =>
            user.BoardMemberships.Any(m => m.BoardId == boardId)
            || user.ProjectMemberships.Any(m => m.Project.Boards.Any(b => b.Id == boardId))
        );

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
