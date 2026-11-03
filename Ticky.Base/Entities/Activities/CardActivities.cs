namespace Ticky.Base.Entities;

// Changes to the card itself: where it is and its own fields.
// Shared column names (Name, OldName, At, ...) are reused across activity kinds so the table stays narrow.

public class CardMovedActivity() : Activity(ActivityType.CardMoved)
{
    public int? FromColumnId { get; set; }
    [JsonIgnore]
    public virtual Column? FromColumn { get; set; }

    /// <summary>Null for moves recorded before the origin was tracked.</summary>
    public string? FromColumnName { get; set; }

    public int? ToColumnId { get; set; }
    [JsonIgnore]
    public virtual Column? ToColumn { get; set; }

    public string? ToColumnName { get; set; }

    /// <summary>Whether the target column counted as done at the time. Null for moves recorded before this was tracked.</summary>
    public bool? ToColumnFinished { get; set; }
}

public class TitleChangedActivity() : Activity(ActivityType.TitleChanged)
{
    /// <summary>Null for changes recorded before titles were kept.</summary>
    [Column("OldName")]
    public string? OldTitle { get; set; }

    [Column("Name")]
    public string? NewTitle { get; set; }
}

public class DescriptionChangedActivity() : Activity(ActivityType.DescriptionChanged);

public class CommentPostedActivity() : Activity(ActivityType.CommentPosted);

public class PriorityChangedActivity() : Activity(ActivityType.PriorityChanged)
{
    [Column("Priority")]
    public CardPriority? Priority { get; set; }
}

public class DeadlineSetActivity() : Activity(ActivityType.DeadlineSet)
{
    [Column("At")]
    public DateTime? Deadline { get; set; }
}

public class DeadlineRemovedActivity() : Activity(ActivityType.DeadlineRemoved);

public class SnoozedActivity() : Activity(ActivityType.Snoozed)
{
    [Column("At")]
    public DateTime? Until { get; set; }
}

public class UnsnoozedActivity() : Activity(ActivityType.Unsnoozed);

public class RepeatSetActivity() : Activity(ActivityType.RepeatSet)
{
    [Column("RepeatType")]
    public RepeatType? RepeatType { get; set; }
}

public class RepeatDisabledActivity() : Activity(ActivityType.RepeatDisabled);
