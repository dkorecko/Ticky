namespace Ticky.Base.Models;

public class ReminderModel
{
    [Required(AllowEmptyStrings = false)]
    [DataType(DataType.DateTime)]
    public DateTime At { get; set; } = DateTime.Now;

    [Required(AllowEmptyStrings = false)]
    [RegularExpression(
        "^([01][0-9]|2[0-3]):[0-5][0-9]$",
        ErrorMessage = "The time must be in HH:mm format."
    )]
    public string Time { get; set; } = string.Empty;
}
