namespace Ticky.Base.Models;

public class FilterCardsModel
{
    public string Text { get; set; } = string.Empty;

    public List<int> AssignedUserIds { get; set; } = [];

    public List<int> LabelIds { get; set; } = [];

    public bool IncludeUnassigned { get; set; } = false;

    public bool IsAnyFilterApplied()
    {
        return !string.IsNullOrWhiteSpace(Text)
            || AssignedUserIds.Count > 0
            || LabelIds.Count > 0
            || IncludeUnassigned;
    }

    public void ClearFilters()
    {
        Text = string.Empty;
        AssignedUserIds.Clear();
        LabelIds.Clear();
        IncludeUnassigned = false;
    }
}
