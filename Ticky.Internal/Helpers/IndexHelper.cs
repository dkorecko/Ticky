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
        where T : IOrderable
    {
        var orderedElements = current.OrderBy(x => x.Index);
        int elements = current.Count;

        for (int i = 0; i < elements; i++)
        {
            if (orderedElements.FirstOrDefault(x => x.Index.Equals(i)) is not null)
                continue;

            var nextElement = orderedElements.First(x => x.Index > i);
            nextElement.Index = i;
        }
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
