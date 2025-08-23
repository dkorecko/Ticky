namespace Ticky.Base.Models;

public class FilterCardsModel
{
    public string Text { get; set; } = string.Empty;

    public List<int> AssignedUserIds { get; set; } = [];

    public bool IncludeUnassigned { get; set; } = false;

    public bool IsAnyFilterApplied()
    {
        return !string.IsNullOrWhiteSpace(Text) || AssignedUserIds.Count > 0 || IncludeUnassigned;
    }

    public void ClearFilters()
    {
        Text = string.Empty;
        AssignedUserIds.Clear();
        IncludeUnassigned = false;
    }
}
