namespace Ticky.Base.DTOs.Api;

// Times: Ticky works in the server's local time. Times sent without an offset are taken as server-local,
// times with an offset (or "Z") are converted to it, and times are returned without an offset.

public class CreateCardRequest
{
    public required int ColumnId { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "A name is required.")]
    [MaxLength(300)]
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Normal when left out.</summary>
    public CardPriority? Priority { get; set; }

    public DateTime? Deadline { get; set; }

    /// <summary>Ids of the board's labels to put on the card.</summary>
    public List<int>? LabelIds { get; set; }

    /// <summary>User ids of board members to assign; see the board's members.</summary>
    public List<int>? AssigneeIds { get; set; }
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
    /// <summary>A column on the card's board; the card's own column to reorder it.</summary>
    public required int TargetColumnId { get; set; }

    /// <summary>The card to put it before, or null to put it at the bottom of the column.</summary>
    public int? BeforeCardId { get; set; }
}

public class AddCommentRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(10_000)]
    public required string Text { get; set; }
}

public class SnoozeCardRequest
{
    /// <summary>When the card comes back on the board.</summary>
    public required DateTime Until { get; set; }
}

public class SetRepeatRequest
{
    public required RepeatType Type { get; set; }

    /// <summary>The time of day copies are created, as <c>hh:mm:ss</c>.</summary>
    public required TimeOnly Time { get; set; }

    /// <summary>The first day the schedule counts from; today when left out.</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>For <c>WeekDays</c>: the days, like <c>["Mon", "Thu"]</c>.</summary>
    public List<string>? WeekDays { get; set; }

    /// <summary>For <c>MonthDayNumber</c>: the days of the month, like <c>[1, 15]</c>.</summary>
    public List<int>? MonthDays { get; set; }

    /// <summary>For the <c>EveryXth…</c> types: every how many days, weeks, months or years (1–999).</summary>
    public int? Interval { get; set; }

    /// <summary>Whether copies go to the top (the default) or the bottom of their column.</summary>
    public CardPlacement Placement { get; set; } = CardPlacement.Top;

    /// <summary>The column on the card's board the copies go to.</summary>
    [Required(ErrorMessage = "Pick the column the copies go to.")]
    public int? TargetColumnId { get; set; }
}

public class AddSubtaskRequest
{
    [Required(AllowEmptyStrings = false)]
    public required string Text { get; set; }
}

/// <summary>Partial update: a missing or null field is left unchanged.</summary>
public class UpdateSubtaskRequest
{
    public string? Text { get; set; }

    public bool? Completed { get; set; }
}

public class MoveSubtaskRequest
{
    /// <summary>The subtask to put it before, or null to put it last.</summary>
    public int? BeforeSubtaskId { get; set; }
}

public class AddReminderRequest
{
    public required DateTime At { get; set; }
}

public class TimeRecordRequest
{
    /// <summary>The time spent, as <c>hh:mm:ss</c> (or <c>d.hh:mm:ss</c>); more than zero, and at most a year.</summary>
    public required TimeSpan Duration { get; set; }
}

public class LinkCardRequest
{
    /// <summary>The card to link to. It may be on another board you can open.</summary>
    public required int TargetCardId { get; set; }

    /// <summary>
    /// The relationship, read as "this card [category] the target card": <c>is blocked by</c>, <c>blocks</c>,
    /// <c>is tested by</c>, <c>tests</c>, <c>relates to</c>, <c>is repeated by</c> or <c>repeats</c>. The target card
    /// gets the opposite.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string Category { get; set; }
}
