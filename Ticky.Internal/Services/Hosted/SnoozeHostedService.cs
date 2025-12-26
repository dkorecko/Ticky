namespace Ticky.Internal.Services.Hosted;

public class SnoozeHostedService(IServiceScopeFactory serviceScopeFactory) : AbstractHostedService<SnoozeHostedService>(
    serviceScopeFactory,
    TimeSpan.FromSeconds(Constants.Limits.MINIMUM_SECOND_HOSTED_SERVICE_DELAY),
    TimeSpan.FromMinutes(2))
{
    protected override async Task OnRun()
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

        if (expiredSnoozes.Count != 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("{ExpiredSnoozesCount} cards unsnoozed.", expiredSnoozes.Count);
        }
    }
}
