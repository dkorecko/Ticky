namespace Ticky.Internal.Services.Hosted;

public class ReminderHostedService(IServiceScopeFactory serviceScopeFactory)
    : AbstractHostedService<ReminderHostedService>(serviceScopeFactory,
        TimeSpan.FromSeconds(Constants.Limits.MINIMUM_SECOND_HOSTED_SERVICE_DELAY),
        TimeSpan.FromMinutes(2))
{
    protected override async Task OnRun()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>()!;
        var mailService = scope.ServiceProvider.GetRequiredService<EmailService>()!;

        var onTimeReminders = await db
            .Reminders.Include(x => x.Card)
            .ThenInclude(x => x.Column)
            .ThenInclude(x => x.Board)
            .Include(x => x.Card)
            .ThenInclude(x => x.Assignees)
            .Include(x => x.Card)
            .ThenInclude(x => x.CreatedBy)
            .Include(x => x.Card)
            .ThenInclude(x => x.Subtasks)
            .Where(x => DateTime.Now > x.At)
            .ToListAsync();

        foreach (var reminder in onTimeReminders)
        {
            // Accounts without an e-mail address can't get reminders.
            var recipients = reminder
                .Card.Assignees.Append(reminder.Card.CreatedBy)
                .Select(x => x.Email)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var email in recipients)
            {
                try
                {
                    await mailService.SendReminderEmailAsync(email, reminder);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to send reminder to {email}. Error: {ex}");
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
            .Include(x => x.Subtasks)
            .Include(x => x.CreatedBy)
            .Where(x =>
                x.Deadline != null
                && !x.DeadlineProcessed
                && x.Deadline.Value.Date.Equals(DateTime.Today.Date)
            )
            .ToListAsync();

        int sentReminders = 0;

        foreach (var card in cardsAtDeadline)
        {
            // Accounts without an e-mail address can't get reminders.
            var recipients = card
                .Assignees.Append(card.CreatedBy)
                .Where(x => x.AutomaticDeadlineReminder)
                .Select(x => x.Email)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var email in recipients)
            {
                try
                {
                    await mailService.SendDeadlineReminderEmailAsync(email, card);
                    sentReminders++;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to send deadline reminder to {email}. Error: {ex}");
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
