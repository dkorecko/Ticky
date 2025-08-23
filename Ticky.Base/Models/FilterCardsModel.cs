namespace Ticky.Base.Models;

public class FilterCardsModel
{
    public string Text { get; set; } = string.Empty;

    public bool IsAnyFilterApplied() => !string.IsNullOrWhiteSpace(Text);
}
