namespace Ticky.Base.Entities;

/// <summary>
/// Something that happened on a card. Each <see cref="Enums.ActivityType"/> has its own subclass holding what is needed
/// to describe it; the text shown to people is built from that data when it is displayed, never stored.
/// </summary>
/// <remarks>
/// All subclasses live in one table (EF Core table-per-hierarchy) with <see cref="ActivityType"/> as the discriminator.
/// Names are copied at the time of the change, so history still reads correctly after something is renamed or deleted.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(GenericActivity), nameof(ActivityType.Generic))]
[JsonDerivedType(typeof(CardMovedActivity), nameof(ActivityType.CardMoved))]
[JsonDerivedType(typeof(TitleChangedActivity), nameof(ActivityType.TitleChanged))]
[JsonDerivedType(typeof(DescriptionChangedActivity), nameof(ActivityType.DescriptionChanged))]
[JsonDerivedType(typeof(CommentPostedActivity), nameof(ActivityType.CommentPosted))]
[JsonDerivedType(typeof(AssigneeAddedActivity), nameof(ActivityType.AssigneeAdded))]
[JsonDerivedType(typeof(AssigneeRemovedActivity), nameof(ActivityType.AssigneeRemoved))]
[JsonDerivedType(typeof(SubtaskAssigneeAddedActivity), nameof(ActivityType.SubtaskAssigneeAdded))]
[JsonDerivedType(typeof(SubtaskAssigneeRemovedActivity), nameof(ActivityType.SubtaskAssigneeRemoved))]
[JsonDerivedType(typeof(LabelAddedActivity), nameof(ActivityType.LabelAdded))]
[JsonDerivedType(typeof(LabelRemovedActivity), nameof(ActivityType.LabelRemoved))]
[JsonDerivedType(typeof(PriorityChangedActivity), nameof(ActivityType.PriorityChanged))]
[JsonDerivedType(typeof(DeadlineSetActivity), nameof(ActivityType.DeadlineSet))]
[JsonDerivedType(typeof(DeadlineRemovedActivity), nameof(ActivityType.DeadlineRemoved))]
[JsonDerivedType(typeof(ReminderAddedActivity), nameof(ActivityType.ReminderAdded))]
[JsonDerivedType(typeof(ReminderRemovedActivity), nameof(ActivityType.ReminderRemoved))]
[JsonDerivedType(typeof(SubtaskAddedActivity), nameof(ActivityType.SubtaskAdded))]
[JsonDerivedType(typeof(SubtaskEditedActivity), nameof(ActivityType.SubtaskEdited))]
[JsonDerivedType(typeof(SubtaskCompletionChangedActivity), nameof(ActivityType.SubtaskCompletionChanged))]
[JsonDerivedType(typeof(SubtaskDeletedActivity), nameof(ActivityType.SubtaskDeleted))]
[JsonDerivedType(typeof(AttachmentUploadedActivity), nameof(ActivityType.AttachmentUploaded))]
[JsonDerivedType(typeof(AttachmentDeletedActivity), nameof(ActivityType.AttachmentDeleted))]
[JsonDerivedType(typeof(TimeRecordAddedActivity), nameof(ActivityType.TimeRecordAdded))]
[JsonDerivedType(typeof(TimeRecordChangedActivity), nameof(ActivityType.TimeRecordChanged))]
[JsonDerivedType(typeof(TimeRecordDeletedActivity), nameof(ActivityType.TimeRecordDeleted))]
[JsonDerivedType(typeof(LinkAddedActivity), nameof(ActivityType.LinkAdded))]
[JsonDerivedType(typeof(LinkRemovedActivity), nameof(ActivityType.LinkRemoved))]
[JsonDerivedType(typeof(SnoozedActivity), nameof(ActivityType.Snoozed))]
[JsonDerivedType(typeof(UnsnoozedActivity), nameof(ActivityType.Unsnoozed))]
[JsonDerivedType(typeof(RepeatSetActivity), nameof(ActivityType.RepeatSet))]
[JsonDerivedType(typeof(RepeatDisabledActivity), nameof(ActivityType.RepeatDisabled))]
[JsonDerivedType(typeof(RepeatedFromActivity), nameof(ActivityType.RepeatedFrom))]
[JsonDerivedType(typeof(RepeatCardCreatedActivity), nameof(ActivityType.RepeatCardCreated))]
public abstract class Activity : AbstractDbEntity
{
    protected Activity(ActivityType type) => ActivityType = type;

    public ActivityType ActivityType { get; private set; }

    /// <summary>The person who did it.</summary>
    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public int CardId { get; set; }
    public virtual Card Card { get; set; } = null!;

    /// <summary>
    /// The HTML text this activity was stored with before activities were typed. Only kept for rows that could not be
    /// converted; it is shown as-is instead of building the text from data.
    /// </summary>
    [Column("Text")]
    public string? LegacyText { get; set; }
}

/// <summary>An old activity whose text did not match any known kind. Only <see cref="Activity.LegacyText"/> is known.</summary>
public class GenericActivity() : Activity(ActivityType.Generic);
