using System.Text.RegularExpressions;

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

    /// <summary>
    /// What's wrong with the schedule, as the field and a message, or null if nothing is. The one rule for what
    /// <see cref="GetNextRepeat"/> can work out: a known type, the days or interval the type needs, and a start that
    /// leaves room for the interval. Saving a repeat checks it, and showing or running one skips a schedule that fails
    /// it (old or hand-edited data), instead of searching forever for a day that never comes or overflowing the date.
    /// </summary>
    public (string Field, string Message)? Validate()
    {
        if (!Enum.IsDefined(Type))
            return (nameof(Type), "Unknown repeat type.");

        (string Field, string Message)? problem = Type switch
        {
            RepeatType.WeekDays when !Regex.IsMatch(Selected ?? string.Empty, Constants.Repeat.WEEK_DAYS_PATTERN) =>
                (nameof(Selected), "Give the days of the week, like Mon,Wed,Fri."),
            RepeatType.MonthDayNumber when !Regex.IsMatch(Selected ?? string.Empty, Constants.Repeat.MONTH_DAYS_PATTERN) =>
                (nameof(Selected), "Give the days of the month, like 1,15,31."),
            RepeatType.EveryXthDay or RepeatType.EveryXthWeek or RepeatType.EveryXthMonth or RepeatType.EveryXthYear
                when Number is not (>= 1 and <= Constants.Repeat.MAX_INTERVAL) =>
                (nameof(Number), $"The interval must be between 1 and {Constants.Repeat.MAX_INTERVAL}."),
            _ => null,
        };

        if (problem is null && LastRepeat > DateTime.Now.AddYears(Constants.Repeat.MAX_YEARS_AHEAD))
            return (nameof(LastRepeat), $"The repeat must start within {Constants.Repeat.MAX_YEARS_AHEAD} years from now.");

        return problem;
    }

    /// <summary>Clears the settings the type doesn't use, e.g. the interval of a weekly repeat.</summary>
    public void ClearUnused()
    {
        if (Type is not (RepeatType.WeekDays or RepeatType.MonthDayNumber))
            Selected = null;

        if (Type is not (RepeatType.EveryXthDay or RepeatType.EveryXthWeek or RepeatType.EveryXthMonth or RepeatType.EveryXthYear))
            Number = null;
    }

    /// <summary>
    /// When the card is copied next: the first matching day at <see cref="Time"/> after <see cref="LastRepeat"/>. Null
    /// when the schedule isn't valid (see <see cref="Validate"/>).
    /// </summary>
    public DateTime? GetNextRepeat()
    {
        if (Validate() is not null)
            return null;

        var startDate = LastRepeat;
        var finalDate = new DateTime(DateOnly.FromDateTime(startDate.Date), Time);

        if (
            (int)Type < (int)RepeatType.EveryXthDay
            && Time < TimeOnly.FromDateTime(LastRepeat)
        )
        {
            finalDate = finalDate.AddDays(1);
        }

        switch (Type)
        {
            case RepeatType.Daily:
                return finalDate;
            case RepeatType.WeekDays:
            {
                var allowedDaysOfWeek = Selected!.Split(',').ToList();

                while (!allowedDaysOfWeek.Any(x => finalDate.DayOfWeek.ToString().Contains(x)))
                    finalDate = finalDate.AddDays(1);

                return finalDate;
            }
            case RepeatType.MonthDayNumber:
            {
                var allowedDaysOfMonth = Selected!.Split(',').ToList();

                while (!allowedDaysOfMonth.Contains(finalDate.Day.ToString()))
                    finalDate = finalDate.AddDays(1);

                return finalDate;
            }
            case RepeatType.EveryXthDay:
                return finalDate.AddDays(Number!.Value);
            case RepeatType.EveryXthWeek:
                return finalDate.AddDays(Number!.Value * 7);
            case RepeatType.EveryXthMonth:
                return finalDate.AddMonths(Number!.Value);
            case RepeatType.EveryXthYear:
                return finalDate.AddYears(Number!.Value);
        }

        throw new Exception("Unresolved repeat type.");
    }

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
