namespace Ticky.Base.Entities.Owned;

[Owned]
public class RepeatInfo
{
    public required RepeatType Type { get; set; }

    public required TimeOnly Time { get; set; }

    public int? Number { get; set; }

    public string? Selected { get; set; }

    public DateTime LastRepeat { get; set; } = DateTime.Now;

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
            RepeatType.AfterXthDay => $"After {Number} days",
            RepeatType.AfterXthWeek => $"After {Number} weeks",
            RepeatType.AfterXthMonth => $"After {Number} months",
            RepeatType.AfterXthYear => $"After {Number} years",
            _ => "No repeat"
        };
}
