namespace Ticky.Base.Enums;

// Values are persisted, so only ever append new ones.
public enum ActivityType
{
    [Display(Name = "Other")]
    Generic = 0,

    [Display(Name = "Moved")]
    CardMoved = 1,

    [Display(Name = "Title changed")]
    TitleChanged = 2,

    [Display(Name = "Description changed")]
    DescriptionChanged = 3,

    [Display(Name = "Comment posted")]
    CommentPosted = 4,

    [Display(Name = "Assignee added")]
    AssigneeAdded = 5,

    [Display(Name = "Assignee removed")]
    AssigneeRemoved = 6,

    [Display(Name = "Subtask assignee added")]
    SubtaskAssigneeAdded = 7,

    [Display(Name = "Subtask assignee removed")]
    SubtaskAssigneeRemoved = 8,

    [Display(Name = "Label added")]
    LabelAdded = 9,

    [Display(Name = "Label removed")]
    LabelRemoved = 10,

    [Display(Name = "Priority changed")]
    PriorityChanged = 11,

    [Display(Name = "Deadline set")]
    DeadlineSet = 12,

    [Display(Name = "Deadline removed")]
    DeadlineRemoved = 13,

    [Display(Name = "Reminder added")]
    ReminderAdded = 14,

    [Display(Name = "Reminder removed")]
    ReminderRemoved = 15,

    [Display(Name = "Subtask added")]
    SubtaskAdded = 16,

    [Display(Name = "Subtask edited")]
    SubtaskEdited = 17,

    [Display(Name = "Subtask completion changed")]
    SubtaskCompletionChanged = 18,

    [Display(Name = "Subtask deleted")]
    SubtaskDeleted = 19,

    [Display(Name = "Attachment uploaded")]
    AttachmentUploaded = 20,

    [Display(Name = "Attachment deleted")]
    AttachmentDeleted = 21,

    [Display(Name = "Time record added")]
    TimeRecordAdded = 22,

    [Display(Name = "Time record changed")]
    TimeRecordChanged = 23,

    [Display(Name = "Time record deleted")]
    TimeRecordDeleted = 24,

    [Display(Name = "Linked issue added")]
    LinkAdded = 25,

    [Display(Name = "Linked issue removed")]
    LinkRemoved = 26,

    [Display(Name = "Snoozed")]
    Snoozed = 27,

    [Display(Name = "Unsnoozed")]
    Unsnoozed = 28,

    [Display(Name = "Repeat set up")]
    RepeatSet = 29,

    [Display(Name = "Repeat turned off")]
    RepeatDisabled = 30,

    [Display(Name = "Repeated from card")]
    RepeatedFrom = 31,

    [Display(Name = "Repeat card created")]
    RepeatCardCreated = 32,
}
