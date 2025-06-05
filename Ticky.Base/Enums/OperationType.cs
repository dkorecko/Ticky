namespace Ticky.Base.Enums;

public enum OperationType
{
    [Display(Name = "added")]
    Added,

    [Display(Name = "edited")]
    Edited,

    [Display(Name = "deleted")]
    Deleted,

    [Display(Name = "had their favorites status changed")]
    Favorited
}
