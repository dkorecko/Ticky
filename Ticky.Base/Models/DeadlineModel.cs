namespace Ticky.Base.Models;

public class DeadlineModel
{
    [Required(AllowEmptyStrings = false)]
    [DataType(DataType.DateTime)]
    public DateTime At { get; set; } = DateTime.Now;

    [Required(AllowEmptyStrings = false)]
    [RegularExpression(
        "^[0-2][0-9]:[0-5][0-9]$",
        ErrorMessage = "The time must be in HH:mm format."
    )]
    public string Time { get; set; } = string.Empty;
}
