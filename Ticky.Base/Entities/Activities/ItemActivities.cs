namespace Ticky.Base.Entities;

// Things added to or removed from a card: people, labels, subtasks, reminders, files, time, links.
// Ids point at the item if it still exists; names are copied so the history reads correctly once it doesn't.

public abstract class AssigneeActivity(ActivityType type) : Activity(type)
{
    [Column("TargetId")]
    public int? AssigneeId { get; set; }

    [Column("Name")]
    public string? AssigneeName { get; set; }

    /// <summary>The person assigned or unassigned themselves ("joined"/"left").</summary>
    public bool IsSelf => AssigneeId == UserId;
}

public class AssigneeAddedActivity() : AssigneeActivity(ActivityType.AssigneeAdded);

public class AssigneeRemovedActivity() : AssigneeActivity(ActivityType.AssigneeRemoved);

public abstract class SubtaskAssigneeActivity(ActivityType type) : Activity(type)
{
    [Column("TargetId")]
    public int? AssigneeId { get; set; }

    [Column("Name")]
    public string? AssigneeName { get; set; }

    /// <summary>Null for changes recorded before the subtask was kept.</summary>
    [Column("SubtaskText")]
    public string? SubtaskText { get; set; }
}

public class SubtaskAssigneeAddedActivity() : SubtaskAssigneeActivity(ActivityType.SubtaskAssigneeAdded);

public class SubtaskAssigneeRemovedActivity() : SubtaskAssigneeActivity(ActivityType.SubtaskAssigneeRemoved);

public abstract class LabelActivity(ActivityType type) : Activity(type)
{
    [Column("TargetId")]
    public int? LabelId { get; set; }

    [Column("Name")]
    public string? LabelName { get; set; }
}

public class LabelAddedActivity() : LabelActivity(ActivityType.LabelAdded);

public class LabelRemovedActivity() : LabelActivity(ActivityType.LabelRemoved);

public abstract class ReminderActivity(ActivityType type) : Activity(type)
{
    [Column("At")]
    public DateTime? At { get; set; }
}

public class ReminderAddedActivity() : ReminderActivity(ActivityType.ReminderAdded);

public class ReminderRemovedActivity() : ReminderActivity(ActivityType.ReminderRemoved);

public abstract class SubtaskActivity(ActivityType type) : Activity(type)
{
    [Column("SubtaskText")]
    public string? SubtaskText { get; set; }
}

public class SubtaskAddedActivity() : SubtaskActivity(ActivityType.SubtaskAdded);

public class SubtaskEditedActivity() : SubtaskActivity(ActivityType.SubtaskEdited)
{
    [Column("OldName")]
    public string? OldSubtaskText { get; set; }
}

public class SubtaskCompletionChangedActivity() : SubtaskActivity(ActivityType.SubtaskCompletionChanged)
{
    [Column("Completed")]
    public bool? Completed { get; set; }
}

public class SubtaskDeletedActivity() : SubtaskActivity(ActivityType.SubtaskDeleted);

public abstract class AttachmentActivity(ActivityType type) : Activity(type)
{
    [Column("Name")]
    public string? FileName { get; set; }
}

public class AttachmentUploadedActivity() : AttachmentActivity(ActivityType.AttachmentUploaded);

public class AttachmentDeletedActivity() : AttachmentActivity(ActivityType.AttachmentDeleted);

public abstract class TimeRecordActivity(ActivityType type) : Activity(type)
{
    [Column("Duration")]
    public TimeSpan? Duration { get; set; }
}

public class TimeRecordAddedActivity() : TimeRecordActivity(ActivityType.TimeRecordAdded);

public class TimeRecordChangedActivity() : TimeRecordActivity(ActivityType.TimeRecordChanged)
{
    [Column("OldDuration")]
    public TimeSpan? OldDuration { get; set; }
}

public class TimeRecordDeletedActivity() : TimeRecordActivity(ActivityType.TimeRecordDeleted);

/// <summary>An activity that mentions another card, by id if it still exists and by its code (e.g. TCK-12).</summary>
public abstract class RelatedCardActivity(ActivityType type) : Activity(type)
{
    [Column("TargetId")]
    public int? RelatedCardId { get; set; }

    [Column("Name")]
    public string? RelatedCardCode { get; set; }
}

public abstract class LinkActivity(ActivityType type) : RelatedCardActivity(type)
{
    /// <summary>The relationship as seen from this card, e.g. "blocks".</summary>
    [Column("Category")]
    public string? Category { get; set; }
}

public class LinkAddedActivity() : LinkActivity(ActivityType.LinkAdded);

public class LinkRemovedActivity() : LinkActivity(ActivityType.LinkRemoved);

/// <summary>This card was created by repeating another one.</summary>
public class RepeatedFromActivity() : RelatedCardActivity(ActivityType.RepeatedFrom);

/// <summary>Repeating this card created another one.</summary>
public class RepeatCardCreatedActivity() : RelatedCardActivity(ActivityType.RepeatCardCreated);
