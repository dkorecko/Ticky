namespace Ticky.Base.Models;

public class SubtaskModel
{
    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Subtask text")]
    public string Text { get; set; } = string.Empty;
}
