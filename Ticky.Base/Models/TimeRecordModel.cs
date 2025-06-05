namespace Ticky.Base.Models;

public class TimeRecordModel
{
    [Display(Name = "Spent time (0h 0m 0s)")]
    [Required(AllowEmptyStrings = false)]
    [IsValidTimeSpan]
    public string Time { get; set; } = string.Empty;
}
