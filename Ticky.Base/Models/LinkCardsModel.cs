namespace Ticky.Base.Models;

public class LinkCardsModel
{
    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Text")]
    public string Text { get; set; } = string.Empty;
}
