namespace Ticky.Internal.Services.Hosted;

public class SnoozeHostedService : AbstractHostedService<SnoozeHostedService>
{
    public SnoozeHostedService(IServiceScopeFactory serviceScopeFactory)
        : base(
            serviceScopeFactory,
            TimeSpan.FromSeconds(Constants.Limits.MINIMUM_SECOND_HOSTED_SERVICE_DELAY),
            TimeSpan.FromMinutes(2)
        ) { }

    protected override async void OnRun()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>()!;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SnoozeHostedService>>()!;

        var expiredSnoozes = await db
            .Cards.Where(x => x.SnoozedUntil != null && x.SnoozedUntil <= DateTime.Now)
            .ToListAsync();

        foreach (var card in expiredSnoozes)
        {
            card.SnoozedUntil = null;
        }

        if (expiredSnoozes.Any())
        {
            await db.SaveChangesAsync();
            logger.LogInformation($"{expiredSnoozes.Count} cards unsnoozed.");
        }
    }
}
