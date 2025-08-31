namespace Ticky.Base.Enums;

public enum OrderRule
{
    [Display(Name = "No automatic ordering")]
    None = 0,

    [Display(Name = "By closest due date")]
    ClosestDueDate = 1,

    [Display(Name = "By latest due date")]
    LatestDueDate = 2,

    [Display(Name = "By highest priority")]
    HighestPriority = 3,

    [Display(Name = "By lowest priority")]
    LowestPriority = 4,

    [Display(Name = "By newest first")]
    NewestFirst = 5,

    [Display(Name = "By oldest first")]
    OldestFirst = 6,

    [Display(Name = "Migrated")]
    Migrated = -1
}
