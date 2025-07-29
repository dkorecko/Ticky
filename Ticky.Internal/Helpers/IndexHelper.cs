namespace Ticky.Internal.Helpers;

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

    public static void ChangeOrderOfItem<T>(this List<T> current, int currentIndex, int newIndex)
        where T : IOrderable
    {
        if (currentIndex == newIndex)
            return;

        var orderedElements = current.OrderBy(x => x.Index).ToList();
        var targetElement = orderedElements.ElementAt(currentIndex);

        orderedElements.RemoveAt(currentIndex);
        orderedElements.Insert(newIndex, targetElement);

        for (int i = 0; i < orderedElements.Count; i++)
        {
            orderedElements.ElementAt(i).Index = i;
        }
    }
}
