using System.ComponentModel;

namespace Ticky.Base.Models;

public class RepeatCardModel
{
    public RepeatType Type { get; set; } = RepeatType.Daily;

    [Display(Name = "Days within month (1,15,31 format)")]
    [RequiredIf(nameof(Type), RepeatType.MonthDayNumber)]
    [RegularExpression(Constants.Repeat.MONTH_DAYS_PATTERN, ErrorMessage = "This field must be in this format: 1,15,31")]
    public string? SelectedMonthDays { get; set; }

    [Display(Name = "Days to repeat on (Mon,Tue,Wed format)")]
    [RequiredIf(nameof(Type), RepeatType.WeekDays)]
    [RegularExpression(Constants.Repeat.WEEK_DAYS_PATTERN, ErrorMessage = "This field must be in this format: Mon,Tue,Wed")]
    public string? SelectedWeekDays { get; set; }

    [RequiredIf(
        nameof(Type),
        RepeatType.EveryXthDay,
        RepeatType.EveryXthWeek,
        RepeatType.EveryXthMonth,
        RepeatType.EveryXthYear
    )]
    [Range(1, Constants.Repeat.MAX_INTERVAL)]
    public int? Number { get; set; }

    [Display(Name = "Start from date")]
    [Required(AllowEmptyStrings = false)]
    [DataType(DataType.Date)]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    [Display(Name = "Time of day for the repeat to occur")]
    [Required(AllowEmptyStrings = false)]
    [RegularExpression(
        "^([01][0-9]|2[0-3]):[0-5][0-9]$",
        ErrorMessage = "The time must be in HH:mm format."
    )]
    public string Time { get; set; } = string.Empty;

    [Display(Name = "Target column for the new card")]
    [Required]
    public int? TargetColumnId { get; set; }

    [Display(Name = "Where in column to place the new card")]
    public CardPlacement CardPlacement { get; set; } = CardPlacement.Top;

    public string? GetRelevantSelectedValue() =>
        Type switch
        {
            RepeatType.MonthDayNumber => SelectedMonthDays,
            RepeatType.WeekDays => SelectedWeekDays,
            _ => null
        };

    public void SetRelevantSelectedValue(string? value)
    {
        switch (Type)
        {
            case RepeatType.MonthDayNumber:
                SelectedMonthDays = value;
                break;
            case RepeatType.WeekDays:
                SelectedWeekDays = value;
                break;
        }
    }
}
