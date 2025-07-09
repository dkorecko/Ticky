namespace Ticky.Base.Entities.Owned;

[Owned]
public class RepeatInfo
{
    public required RepeatType Type { get; set; }

    public required TimeOnly Time { get; set; }

    public int? Number { get; set; }

    public string? Selected { get; set; }

    public DateTime LastRepeat { get; set; } = DateTime.Now;

    public required CardPlacement CardPlacement { get; set; }

    public int? TargetColumnId { get; set; }

    public string GetRepeatString() =>
        Type switch
        {
            RepeatType.Daily => "Daily",
            RepeatType.WeekDays => $"On each {string.Join(", ", Selected!.Split(','))}",
            RepeatType.MonthDayNumber
                => $"On day {string.Join(", ", Selected!.Split(','))} of the month",
            RepeatType.EveryXthDay => $"Every {Number} days",
            RepeatType.EveryXthWeek => $"Every {Number} weeks",
            RepeatType.EveryXthMonth => $"Every {Number} months",
            RepeatType.EveryXthYear => $"Every {Number} years",
            _ => "No repeat"
        };
}
