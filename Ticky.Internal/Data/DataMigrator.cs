namespace Ticky.Internal.Data;

public class DataMigrator
{
    public static async Task Seed(IServiceProvider serviceProvider)
    {
        var dataContext = serviceProvider.GetRequiredService<DataContext>();

        var topRules = new HashSet<OrderRule>
        {
            OrderRule.ClosestDueDate,
            OrderRule.HighestPriority,
            OrderRule.NewestFirst
        };

        await dataContext
            .Columns.Where(c => c.OrderRule != OrderRule.Migrated && topRules.Contains(c.OrderRule))
            .ExecuteUpdateAsync(s =>
                s.SetProperty(c => c.NewCardPlacement, CardPlacement.Top)
                    .SetProperty(c => c.OrderRule, OrderRule.Migrated)
            );

        await dataContext
            .Columns.Where(c =>
                c.OrderRule != OrderRule.Migrated && !topRules.Contains(c.OrderRule)
            )
            .ExecuteUpdateAsync(s =>
                s.SetProperty(c => c.NewCardPlacement, CardPlacement.Bottom)
                    .SetProperty(c => c.OrderRule, OrderRule.Migrated)
            );
    }
}
