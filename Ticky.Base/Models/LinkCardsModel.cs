namespace Ticky.Base.Models;

public class LinkCardsModel
{
    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Text")]
    public string Text { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Card identifier to link with this card")]
    public string TargetCardId { get; set; } = string.Empty;
}
