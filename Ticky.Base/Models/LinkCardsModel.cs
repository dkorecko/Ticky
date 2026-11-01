namespace Ticky.Base.Models;

public class LinkCardsModel
{
    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Link type")]
    public string Text { get; set; } = string.Empty;
}
