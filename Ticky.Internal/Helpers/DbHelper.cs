namespace Ticky.Internal.Helpers;

public static class DbHelper
{
    public static IQueryable<Card> IncludeCardViewRequiredProperties(this IQueryable<Card> query) =>
        query
            .Include(x => x.Labels)
            .Include(x => x.CreatedBy)
            .Include(x => x.Assignees)
            .Include(x => x.Attachments)
            .Include(x => x.Subtasks)
            .Include(x => x.TimeRecords);
}
