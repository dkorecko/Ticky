namespace Ticky.Base.Enums;

public enum RepeatType
{
    [Display(Name = "Daily")]
    Daily = 1,

    [Display(Name = "On specific days of the week (Monday, Tuesday, ...)")]
    WeekDays = 10,

    [Display(Name = "On specific days of the month (eg. 1st, 15th, ...)")]
    MonthDayNumber = 20,

    // Make sure order is correct, all above this line handle days differently from the ones below
    [Display(Name = "Every x days")]
    EveryXthDay = 100,

    [Display(Name = "Every x weeks")]
    EveryXthWeek = 110,

    [Display(Name = "Every x months")]
    EveryXthMonth = 120,

    [Display(Name = "Every x years")]
    EveryXthYear = 130
}
