using Ticky.Base.Enums;

namespace Ticky.Base.DTOs.Api;

public class CreateCardRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "A column is required.")]
    public required int ColumnId { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "A name is required.")]
    [MaxLength(300)]
    public required string Name { get; set; }
}

/// <summary>
/// Partial update: each field is optional. Null means "don't change"; an explicit value — including
/// an explicit null for <see cref="Deadline"/> — is applied. Use dedicated endpoints for moves and
/// membership changes rather than overloading this.
/// </summary>
public class UpdateCardRequest
{
    [MaxLength(300)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public CardPriority? Priority { get; set; }

    /// <summary>Set to clear-deadline via an explicit <c>"clearDeadline": true</c> (since null means "no change").</summary>
    public DateTime? Deadline { get; set; }

    public bool? ClearDeadline { get; set; }

    public bool? Flagged { get; set; }
}

public class MoveCardRequest
{
    [Required]
    public required int TargetColumnId { get; set; }

    /// <summary>Card to insert before, or null to append at the end of the target column.</summary>
    public int? BeforeCardId { get; set; }
}

public class AddCommentRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(10_000)]
    public required string Text { get; set; }
}
