namespace Ticky.Base.Enums;

public enum RepeatType
{
    [Display(Name = "Daily")]
    Daily = 1,

    [Display(Name = "On specific days of the week (Monday, Tuesday, ...)")]
    WeekDays = 10,

    [Display(Name = "On specific days of the month (eg. 1st, 15th, ...)")]
    MonthDayNumber = 20,

    [Display(Name = "Every x days")]
    EveryXthDay = 100,

    [Display(Name = "Every x weeks")]
    EveryXthWeek = 110,

    [Display(Name = "Every x months")]
    EveryXthMonth = 120,

    [Display(Name = "Every x years")]
    EveryXthYear = 130,

    [Display(Name = "After x days")]
    AfterXthDay = 140,

    [Display(Name = "After x weeks")]
    AfterXthWeek = 150,

    [Display(Name = "After x months")]
    AfterXthMonth = 160,

    [Display(Name = "After x years")]
    AfterXthYear = 170
}
