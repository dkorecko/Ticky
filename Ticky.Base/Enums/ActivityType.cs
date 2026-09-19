namespace Ticky.Base.Enums;

// Values are persisted, so only ever append new ones.
public enum ActivityType
{
    [Display(Name = "Other")]
    Generic = 0,

    [Display(Name = "Moved")]
    CardMoved = 1,
}
