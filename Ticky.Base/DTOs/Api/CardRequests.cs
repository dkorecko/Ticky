using Ticky.Base.Enums;

namespace Ticky.Base.DTOs.Api;

// Times: Ticky works in the server's local time. Times sent without an offset are taken as server-local,
// times with an offset (or "Z") are converted to it, and times are returned without an offset.

public class CreateCardRequest
{
    public required int ColumnId { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "A name is required.")]
    [MaxLength(300)]
    public required string Name { get; set; }
}

/// <summary>
/// Partial update: each field is optional, and a missing or null field is left unchanged. To remove the
/// deadline, send <c>"clearDeadline": true</c>, which wins over <see cref="Deadline"/>. Moving cards has
/// its own endpoint.
/// </summary>
public class UpdateCardRequest
{
    [MaxLength(300)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public CardPriority? Priority { get; set; }

    public DateTime? Deadline { get; set; }

    public bool? ClearDeadline { get; set; }

    public bool? Flagged { get; set; }
}

public class MoveCardRequest
{
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
