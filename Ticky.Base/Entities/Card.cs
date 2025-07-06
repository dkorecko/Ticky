using Ticky.Base.Entities.Owned;

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

    public RepeatInfo? RepeatInfo { get; set; }

    public DateTime? SnoozedUntil { get; set; }

    public DateTime CalculateNextRepeat()
    {
        if (RepeatInfo is null)
            throw new Exception("Cannot calculate next repeat on card with no repeat.");

        var startDate = RepeatInfo.LastRepeat;
        var finalDate = new DateTime(DateOnly.FromDateTime(startDate.Date), RepeatInfo.Time);

        var targetLastCreated = LinkedIssuesOne
            .Concat(LinkedIssuesTwo)
            .Where(x => x.Category.Equals(Constants.REPEATED_KEY))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        // This will only return if the card is a repeat of another card and is an after type
        if (targetLastCreated is not null)
        {
            var originalDate = new DateTime(
                DateOnly.FromDateTime(targetLastCreated.CreatedAt.Date),
                RepeatInfo.Time
            );

            switch (RepeatInfo.Type)
            {
                case RepeatType.AfterXthDay:
                    return originalDate.AddDays(RepeatInfo.Number!.Value);
                case RepeatType.AfterXthWeek:
                    return originalDate.AddDays(RepeatInfo.Number!.Value * 7);
                case RepeatType.AfterXthMonth:
                    return originalDate.AddMonths(RepeatInfo.Number!.Value);
                case RepeatType.AfterXthYear:
                    return originalDate.AddYears(RepeatInfo.Number!.Value);
            }
        }

        switch (RepeatInfo.Type)
        {
            case RepeatType.Daily:
                return finalDate.AddDays(1);
            case RepeatType.WeekDays:
            {
                var allowedDaysOfWeek = RepeatInfo.Selected!.Split(',').Select(x => x).ToList();

                while (!allowedDaysOfWeek.Any(x => finalDate.DayOfWeek.ToString().Contains(x)))
                    finalDate = finalDate.AddDays(1);

                return finalDate;
            }
            case RepeatType.MonthDayNumber:
            {
                var allowedDaysOfMonth = RepeatInfo.Selected!.Split(',').Select(x => x).ToList();

                while (!allowedDaysOfMonth.Contains(finalDate.Day.ToString()))
                    finalDate = finalDate.AddDays(1);

                break;
            }
            case RepeatType.EveryXthDay:
            case RepeatType.AfterXthDay:
                return finalDate.AddDays(RepeatInfo.Number!.Value);
            case RepeatType.EveryXthWeek:
            case RepeatType.AfterXthWeek:
                return finalDate.AddDays(RepeatInfo.Number!.Value * 7);
            case RepeatType.EveryXthMonth:
            case RepeatType.AfterXthMonth:
                return finalDate.AddMonths(RepeatInfo.Number!.Value);
            case RepeatType.EveryXthYear:
            case RepeatType.AfterXthYear:
                return finalDate.AddYears(RepeatInfo.Number!.Value);
        }

        throw new Exception("Unresolved repeat type.");
    }
}
