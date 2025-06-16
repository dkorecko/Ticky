namespace Ticky.Base.Entities;

public class Card : AbstractDbEntity, IOrderable, IDeletable
{
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public required int Number { get; set; }
    public required int Index { get; set; }
    public required int ColumnId { get; set; }
    public CardPriority Priority { get; set; } = CardPriority.Normal;
    public DateTime? Deadline { get; set; }
    public bool DeadlineProcessed { get; set; }
    public bool Blocked { get; set; }
    public required int CreatedById { get; set; }
    public virtual User CreatedBy { get; set; } = null!;
    public virtual Column Column { get; set; } = null!;
    public virtual List<Comment> Comments { get; set; } = [];
    public virtual List<User> Assignees { get; set; } = [];
    public virtual List<Attachment> Attachments { get; set; } = [];
    public virtual List<Activity> Activities { get; set; } = [];
    public virtual List<Subtask> Subtasks { get; set; } = [];
    public virtual List<Reminder> Reminders { get; set; } = [];
    public virtual List<Label> Labels { get; set; } = [];
    public virtual List<TimeRecord> TimeRecords { get; set; } = [];
    public virtual List<CardLink> LinkedIssuesOne { get; set; } = [];
    public virtual List<CardLink> LinkedIssuesTwo { get; set; } = [];
}
