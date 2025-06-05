namespace Ticky.Internal.Helpers;

public static class ColumnHelper
{
    public static void ReorderCards(this Column column)
    {
        if (column.OrderRule == OrderRule.None)
            return;

        if (column.OrderRule == OrderRule.NewestFirst)
            column.Cards = column.Cards.OrderByDescending(x => x.CreatedAt).ToList();
        else if (column.OrderRule == OrderRule.OldestFirst)
            column.Cards = column.Cards.OrderBy(x => x.CreatedAt).ToList();
        else if (column.OrderRule == OrderRule.HighestPriority)
            column.Cards = column.Cards.OrderByDescending(x => x.Priority).ToList();
        else if (column.OrderRule == OrderRule.LowestPriority)
            column.Cards = column.Cards.OrderBy(x => x.Priority).ToList();
        else if (column.OrderRule == OrderRule.ClosestDueDate)
            column.Cards = column
                .Cards.OrderByDescending(x => x.Deadline != null)
                .ThenBy(x => x.Deadline)
                .ThenByDescending(x => x.Priority)
                .ToList();
        else if (column.OrderRule == OrderRule.LatestDueDate)
            column.Cards = column
                .Cards.OrderByDescending(x => x.Deadline != null)
                .ThenByDescending(x => x.Deadline)
                .ThenByDescending(x => x.Priority)
                .ToList();

        int index = 0;
        foreach (var item in column.Cards)
        {
            item.Index = index;
            index++;
        }
    }
}
