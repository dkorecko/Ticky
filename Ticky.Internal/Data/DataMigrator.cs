namespace Ticky.Internal.Data;

public class DataMigrator
{
    public static async Task Seed(IServiceProvider serviceProvider)
    {
        var dataContext = serviceProvider.GetRequiredService<DataContext>();

        var unmigratedColumns = await dataContext
            .Columns.Where(c => c.OrderRule != OrderRule.Migrated)
            .ToListAsync();

        if (!unmigratedColumns.Any())
            return;

        foreach (var column in unmigratedColumns)
        {
            switch (column.OrderRule)
            {
                case OrderRule.ClosestDueDate:
                case OrderRule.HighestPriority:
                case OrderRule.NewestFirst:
                    column.NewCardPlacement = CardPlacement.Top;
                    break;
                default:
                    column.NewCardPlacement = CardPlacement.Bottom;
                    break;
            }

            column.OrderRule = OrderRule.Migrated;
        }

        await dataContext.SaveChangesAsync();
    }
}
