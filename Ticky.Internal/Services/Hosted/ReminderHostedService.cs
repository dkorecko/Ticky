namespace Ticky.Internal.Services.Hosted;

public class ReminderHostedService : AbstractHostedService<ReminderHostedService>
{
    public ReminderHostedService(IServiceScopeFactory serviceScopeFactory)
        : base(
            serviceScopeFactory,
            TimeSpan.FromSeconds(Constants.Limits.MINIMUM_SECOND_HOSTED_SERVICE_DELAY),
            TimeSpan.FromMinutes(2)
        ) { }

    protected override async void OnRun()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<DataContext>()!;
        var mailService = scope.ServiceProvider.GetService<MailService>()!;

        var onTimeReminders = await db
            .Reminders.Include(x => x.Card)
            .ThenInclude(x => x.Column)
            .ThenInclude(x => x.Board)
            .Include(x => x.Card)
            .ThenInclude(x => x.Assignees)
            .Where(x => DateTime.Now > x.At)
            .ToListAsync();

        foreach (var reminder in onTimeReminders)
        {
            foreach (var assignee in reminder.Card.Assignees)
            {
                try
                {
                    await mailService.SendReminderEmailAsync(assignee.Email!, reminder);
                }
                catch (Exception ex)
                {
                    Logger.LogError(
                        $"Failed to send reminder to user {assignee.Email}. Error: {ex}"
                    );
                }
            }
        }

        if (onTimeReminders.Any())
        {
            db.Reminders.RemoveRange(onTimeReminders);
            await db.SaveChangesAsync();
            Logger.LogInformation($"{onTimeReminders.Count} reminders have been sent.");
        }

        var cardsAtDeadline = await db
            .Cards.Include(x => x.Column)
            .ThenInclude(x => x.Board)
            .Include(x => x.Assignees)
            .Where(x =>
                x.Deadline != null
                && !x.DeadlineProcessed
                && x.Deadline.Value.Date.Equals(DateTime.Today.Date)
                && DateTime.Compare(x.Deadline.Value, DateTime.Now) > 0
            )
            .ToListAsync();

        int sentReminders = 0;

        foreach (var card in cardsAtDeadline)
        {
            foreach (var assignee in card.Assignees)
            {
                if (!assignee.AutomaticDeadlineReminder)
                    continue;

                try
                {
                    await mailService.SendDeadlineReminderEmailAsync(assignee.Email!, card);
                    sentReminders++;
                }
                catch (Exception ex)
                {
                    Logger.LogError(
                        $"Failed to send deadline reminder to user {assignee.Email}. Error: {ex}"
                    );
                }
            }

            card.DeadlineProcessed = true;
        }

        if (cardsAtDeadline.Any())
        {
            await db.SaveChangesAsync();
            Logger.LogInformation($"{cardsAtDeadline.Count} deadline reminders have been sent.");
        }
    }
}
