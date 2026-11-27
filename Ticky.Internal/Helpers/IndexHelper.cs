namespace Ticky.Internal.Helpers;

/// <summary>Keeps ordered items (columns, cards, subtasks) numbered 0, 1, 2, … in their order.</summary>
public static class IndexHelper
{
    public static int GetNextIndex<T>(this List<T> current)
        where T : IOrderable
    {
        var result = current.Max(x => (int?)x.Index);

        if (result is null)
            return 0;

        return (int)result + 1;
    }

    public static void FixIndices<T>(this List<T> current)
        where T : IOrderable, IDbEntry
    {
        var orderedElements = current.OrderBy(x => x.Index).ThenBy(x => x.Id).ToList();

        for (int i = 0; i < orderedElements.Count; i++)
            orderedElements[i].Index = i;
    }

    /// <summary>
    /// Puts <paramref name="item"/> right before <paramref name="before"/>, or last when that is null or not in the
    /// list, and numbers the list again. The item may already be in the list (reordering) or not (moving it in from
    /// another list, which the caller adds it to).
    /// </summary>
    public static void MoveBefore<T>(this List<T> items, T item, T? before)
        where T : class, IOrderable, IDbEntry
    {
        // Before itself: it stays where it is.
        if (before == item)
            return;

        var ordered = items.Where(x => x != item).OrderBy(x => x.Index).ThenBy(x => x.Id).ToList();
        var position = before is null ? -1 : ordered.IndexOf(before);

        ordered.Insert(position < 0 ? ordered.Count : position, item);

        for (int i = 0; i < ordered.Count; i++)
            ordered[i].Index = i;
    }

    /// <summary>Adds a new card to the top or bottom of a column's cards, as the column or repeat says.</summary>
    public static void Place(this List<Card> columnCards, Card newCard, CardPlacement placement)
    {
        switch (placement)
        {
            case CardPlacement.Top:
                columnCards.ForEach(x => x.Index++);
                newCard.Index = 0;
                break;
            case CardPlacement.Bottom:
                newCard.Index = columnCards.GetNextIndex();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(placement), placement, "Unsupported card placement.");
        }

        columnCards.Add(newCard);
    }
}
