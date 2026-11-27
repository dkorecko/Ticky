using System.Linq.Expressions;
using System.Text.Json;
using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// How entities become the API's response shapes, in one place. The projections run as part of the database query,
/// so only the fields and counts a response needs are read. Boards and projects are mapped after loading instead,
/// because who administers them is decided by <see cref="Board.IsAdmin(int)"/>.
/// </summary>
public static class ApiMappings
{
    #region Boards and projects

    /// <summary>
    /// A board as listed. Needs the board's and its project's memberships loaded, and the user's favorite of it (see
    /// <see cref="WithAccessDetails"/>).
    /// </summary>
    public static BoardSummaryDTO ToSummaryDto(this Board board, int userId) =>
        new(
            board.Id,
            board.Name,
            board.Description,
            board.Code,
            board.ProjectId,
            board.Project.Name,
            board.IsAdmin(userId),
            board.Favorites.Any(x => x.UserId == userId)
        );

    /// <summary>
    /// A board with its columns, labels and members. Needs what <see cref="ToSummaryDto"/> does, with the members'
    /// users, plus the labels.
    /// </summary>
    public static BoardDetailDTO ToDetailDto(this Board board, int userId, IReadOnlyList<ColumnDTO> columns) =>
        new(
            board.Id,
            board.Name,
            board.Description,
            board.Code,
            board.ProjectId,
            board.Project.Name,
            board.IsAdmin(userId),
            board.Favorites.Any(x => x.UserId == userId),
            board.DisableSortingAnimations,
            columns,
            board.Labels.OrderBy(x => x.Id).Select(ToDto).ToList(),
            board.ToMemberDtos()
        );

    /// <summary>
    /// Everyone who can open the board: its members, then its project's members without a board membership. A board
    /// membership decides the role, as in <see cref="Board.IsAdmin(int)"/>. Admins first, then by name.
    /// </summary>
    public static List<MemberDTO> ToMemberDtos(this Board board) =>
        board.Memberships
            .Select(x => x.ToDto(MembershipSource.Board))
            .Concat(
                board.Project.Memberships
                    .Where(x => board.Memberships.All(m => m.UserId != x.UserId))
                    .Select(x => x.ToDto(MembershipSource.Project))
            )
            .OrderByDescending(x => x.IsAdmin)
            .ThenBy(x => x.DisplayName)
            .ToList();

    /// <summary>A project's members, admins first. Needs the memberships' users loaded.</summary>
    public static List<MemberDTO> ToMemberDtos(this Project project) =>
        project.Memberships
            .Select(x => x.ToDto(MembershipSource.Project))
            .OrderByDescending(x => x.IsAdmin)
            .ThenBy(x => x.DisplayName)
            .ToList();

    public static MemberDTO ToDto(this IMembership membership, MembershipSource source) =>
        new(membership.UserId, membership.User.DisplayName, membership.User.Email, membership.IsAdmin, source);

    /// <summary>
    /// Loads what <see cref="ToSummaryDto"/> and <see cref="ToDetailDto"/> need: memberships with their users, and the
    /// user's own favorite.
    /// </summary>
    public static IQueryable<Board> WithAccessDetails(this IQueryable<Board> boards, int userId) =>
        boards
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Memberships)
                .ThenInclude(x => x.User)
            .Include(x => x.Project)
                .ThenInclude(x => x.Memberships)
                    .ThenInclude(x => x.User)
            .Include(x => x.Favorites.Where(f => f.UserId == userId));

    public static LabelDTO ToDto(this Label label) =>
        new(label.Id, label.Name, label.BackgroundColor.ToColorInputValue(), label.TextColor.ToColorInputValue());

    public static readonly Expression<Func<Column, ColumnDTO>> ColumnProjection = column =>
        new ColumnDTO(
            column.Id,
            column.BoardId,
            column.Name,
            column.Index,
            column.Finished,
            column.MaxCards,
            column.NewCardPlacement,
            column.Collapsed,
            column.Cards.Count
        );

    #endregion

    #region Cards

    public static readonly Expression<Func<Comment, CommentDTO>> CommentProjection = comment =>
        new CommentDTO(comment.Id, comment.Text, comment.CreatedAt, comment.CreatedById, comment.CreatedBy.DisplayName);

    public static readonly Expression<Func<TimeRecord, TimeRecordDTO>> TimeRecordProjection = record =>
        new TimeRecordDTO(record.Id, record.CardId, record.UserId, record.User.DisplayName, record.StartedAt, record.EndedAt);

    /// <summary>A link as seen from the card it starts at (<see cref="CardLink.CardOne"/>).</summary>
    public static readonly Expression<Func<CardLink, CardLinkDTO>> CardLinkProjection = link =>
        new CardLinkDTO(
            link.Id,
            link.Category,
            link.CardTwoId,
            link.CardTwo.Column.Board.Code + "-" + link.CardTwo.Number,
            link.CardTwo.Name,
            link.CardTwo.Column.BoardId
        );

    public static readonly Expression<Func<Card, CardSummaryDTO>> CardSummaryProjection = card =>
        new CardSummaryDTO(
            card.Id,
            card.Number,
            card.Column.Board.Code + "-" + card.Number,
            card.Name,
            card.Column.BoardId,
            card.Column.Board.Name,
            card.ColumnId,
            card.Column.Name,
            card.Index,
            card.Priority,
            card.Deadline,
            card.Flagged,
            card.SnoozedUntil,
            card.Assignees.Select(a => a.Id).ToList(),
            card.Labels.Select(l => l.Id).ToList(),
            card.Subtasks.Count,
            card.Subtasks.Count(s => s.Completed),
            card.Attachments.Count,
            card.Comments.Count
        );

    public static readonly Expression<Func<Card, CardDetailDTO>> CardDetailProjection = card =>
        new CardDetailDTO(
            card.Id,
            card.Number,
            card.Column.Board.Code + "-" + card.Number,
            card.Name,
            card.Description,
            card.Column.BoardId,
            card.Column.Board.Name,
            card.ColumnId,
            card.Column.Name,
            card.Index,
            card.Priority,
            card.Deadline,
            card.Flagged,
            card.SnoozedUntil,
            card.RepeatInfo == null ? null : card.RepeatInfo.ToDto(),
            card.CreatedAt,
            card.CreatedById,
            card.Assignees.OrderBy(a => a.DisplayName).Select(a => new CardAssigneeDTO(a.Id, a.DisplayName)).ToList(),
            card.Labels.OrderBy(l => l.Id).Select(l => l.ToDto()).ToList(),
            card.Subtasks
                .OrderBy(s => s.Index)
                .Select(s => new SubtaskDTO(s.Id, s.Text, s.Completed, s.Index, s.Assignees.Select(a => a.Id).ToList()))
                .ToList(),
            card.Comments.OrderByDescending(c => c.CreatedAt).AsQueryable().Select(CommentProjection).ToList(),
            card.Reminders.OrderBy(r => r.At).Select(r => new ReminderDTO(r.Id, r.At)).ToList(),
            card.Attachments.OrderBy(a => a.Id).Select(a => new AttachmentDTO(a.Id, a.CardId, a.OriginalName, a.CreatedAt)).ToList(),
            card.LinkedIssuesOne.OrderBy(l => l.Id).AsQueryable().Select(CardLinkProjection).ToList(),
            card.TimeRecords.OrderBy(t => t.StartedAt).AsQueryable().Select(TimeRecordProjection).ToList()
        );

    public static SubtaskDTO ToDto(this Subtask subtask) =>
        new(subtask.Id, subtask.Text, subtask.Completed, subtask.Index, subtask.Assignees.Select(x => x.Id).ToList());

    public static ReminderDTO ToDto(this Reminder reminder) => new(reminder.Id, reminder.At);

    public static AttachmentDTO ToDto(this Attachment attachment) =>
        new(attachment.Id, attachment.CardId, attachment.OriginalName, attachment.CreatedAt);

    public static CardAssigneeDTO ToAssigneeDto(this User user) => new(user.Id, user.DisplayName);

    /// <summary>How the card repeats, with the day lists split out of the stored text.</summary>
    public static RepeatDTO ToDto(this RepeatInfo repeat)
    {
        var days = repeat.Selected?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        return new(
            repeat.Type,
            repeat.Time,
            repeat.Type == RepeatType.WeekDays ? days : null,
            repeat.Type == RepeatType.MonthDayNumber ? days.Select(x => int.TryParse(x, out var day) ? day : 0).ToList() : null,
            repeat.Number,
            repeat.CardPlacement,
            repeat.TargetColumnId,
            repeat.LastRepeat,
            repeat.GetNextRepeat()
        );
    }

    /// <summary>An activity entry. Needs the activity's user and its card's column and board loaded.</summary>
    public static ActivityDTO ToDto(this Activity activity) =>
        new(
            activity.Id,
            activity.ActivityType,
            activity.CardId,
            $"{activity.Card.Column.Board.Code}-{activity.Card.Number}",
            activity.Card.Name,
            activity.UserId,
            activity.User.DisplayName,
            activity.CreatedAt,
            activity.ToText(),
            activity.ToDetails().ToDictionary(x => JsonNamingPolicy.CamelCase.ConvertName(x.Key), x => x.Value)
        );

    #endregion
}
