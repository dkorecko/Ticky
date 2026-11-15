using System.Linq.Expressions;
using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// How entities become the API's response shapes, in one place. The projections run as part of the database query,
/// so only the fields and counts a response needs are read. Boards are mapped after loading instead, because whether
/// the caller administers one is decided by <see cref="Board.IsAdmin(int)"/>.
/// </summary>
public static class ApiMappings
{
    /// <summary>A board as listed. Needs the board's and its project's memberships loaded.</summary>
    public static BoardSummaryDTO ToSummaryDto(this Board board, int userId) =>
        new(board.Id, board.Name, board.Description, board.Code, board.ProjectId, board.Project.Name, board.IsAdmin(userId));

    /// <summary>A board with its columns and labels. Needs what <see cref="ToSummaryDto"/> does, plus the labels.</summary>
    public static BoardDetailDTO ToDetailDto(this Board board, int userId, IReadOnlyList<ColumnDTO> columns) =>
        new(
            board.Id,
            board.Name,
            board.Description,
            board.Code,
            board.ProjectId,
            board.Project.Name,
            board.IsAdmin(userId),
            columns,
            board.Labels.Select(ToDto).ToList()
        );

    public static LabelDTO ToDto(this Label label) =>
        new(label.Id, label.Name, label.BackgroundColor.ToColorInputValue(), label.TextColor.ToColorInputValue());

    public static readonly Expression<Func<Column, ColumnDTO>> ColumnProjection = column =>
        new ColumnDTO(column.Id, column.Name, column.Index, column.Finished, column.Cards.Count);

    public static readonly Expression<Func<Comment, CardCommentDTO>> CommentProjection = comment =>
        new CardCommentDTO(comment.Id, comment.Text, comment.CreatedAt, comment.CreatedById, comment.CreatedBy.DisplayName);

    public static readonly Expression<Func<Card, CardSummaryDTO>> CardSummaryProjection = card =>
        new CardSummaryDTO(
            card.Id,
            card.Number,
            CardKey(card.Column.Board.Code, card.Number),
            card.Name,
            card.ColumnId,
            card.Column.Name,
            card.Index,
            card.Priority,
            card.Deadline,
            card.Flagged,
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
            CardKey(card.Column.Board.Code, card.Number),
            card.Name,
            card.Description,
            card.ColumnId,
            card.Column.Name,
            card.Column.BoardId,
            card.Index,
            card.Priority,
            card.Deadline,
            card.Flagged,
            card.SnoozedUntil,
            card.CreatedAt,
            card.CreatedById,
            card.Assignees.Select(a => new CardAssigneeDTO(a.Id, a.DisplayName)).ToList(),
            card.Labels.Select(l => l.ToDto()).ToList(),
            card.Subtasks.OrderBy(s => s.Index).Select(s => new CardSubtaskDTO(s.Id, s.Text, s.Completed, s.Index)).ToList(),
            card.Comments.OrderByDescending(c => c.CreatedAt).AsQueryable().Select(CommentProjection).ToList()
        );

    /// <summary>The key people know a card by, like <c>TKY-12</c>.</summary>
    private static string CardKey(string boardCode, int number) => $"{boardCode}-{number}";
}
