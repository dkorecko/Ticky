using Ticky.Base.Enums;

namespace Ticky.Base.DTOs.Api;

/// <summary>Compact shape returned by board-cards listings.</summary>
public record CardSummaryDTO(
    int Id,
    int Number,
    string Code,
    string Name,
    int ColumnId,
    string ColumnName,
    int Index,
    CardPriority Priority,
    DateTime? Deadline,
    bool Flagged,
    IReadOnlyList<int> AssigneeIds,
    IReadOnlyList<int> LabelIds,
    int SubtaskCount,
    int SubtaskCompletedCount,
    int AttachmentCount,
    int CommentCount
);

/// <summary>Full card shape returned by <c>GET /api/v1/cards/{id}</c>.</summary>
public record CardDetailDTO(
    int Id,
    int Number,
    string Code,
    string Name,
    string Description,
    int ColumnId,
    string ColumnName,
    int BoardId,
    int Index,
    CardPriority Priority,
    DateTime? Deadline,
    bool Flagged,
    DateTime? SnoozedUntil,
    DateTime CreatedAt,
    int CreatedById,
    IReadOnlyList<CardAssigneeDTO> Assignees,
    IReadOnlyList<LabelDTO> Labels,
    IReadOnlyList<CardSubtaskDTO> Subtasks,
    IReadOnlyList<CardCommentDTO> Comments
);

public record CardAssigneeDTO(int Id, string DisplayName);

public record CardSubtaskDTO(int Id, string Text, bool Completed, int Index);

public record CardCommentDTO(int Id, string Text, DateTime CreatedAt, int CreatedById, string CreatedByName);
