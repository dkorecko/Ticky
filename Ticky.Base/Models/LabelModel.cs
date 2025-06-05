namespace Ticky.Base.Models;

public class LabelModel
{
    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Label text")]
    public string Text { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Text color")]
    public Color? TextColor { get; set; }

    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Background color")]
    public Color? BackgroundColor { get; set; }
}
