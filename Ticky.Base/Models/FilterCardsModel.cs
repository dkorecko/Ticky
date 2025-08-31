namespace Ticky.Base.Models;

public class FilterCardsModel
{
    public string Text { get; set; } = string.Empty;

    public HashSet<int> AssignedUserIds { get; set; } = [];

    public HashSet<int> LabelIds { get; set; } = [];

    public bool IncludeUnassigned { get; set; }

    public bool ExcludeCompleted { get; set; }

    public bool ExpandAssignedUsersSection { get; set; }

    public bool ExpandLabelsSection { get; set; }

    public bool IsAnyFilterApplied()
    {
        return !string.IsNullOrWhiteSpace(Text)
            || AssignedUserIds.Count > 0
            || LabelIds.Count > 0
            || IncludeUnassigned
            || ExcludeCompleted;
    }

    public void ClearFilters()
    {
        Text = string.Empty;
        AssignedUserIds.Clear();
        LabelIds.Clear();
        IncludeUnassigned = false;
        ExcludeCompleted = false;
    }
}
