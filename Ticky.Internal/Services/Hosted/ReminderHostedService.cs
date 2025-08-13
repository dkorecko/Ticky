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
        var mailService = scope.ServiceProvider.GetService<EmailService>()!;

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
            var recipients = new HashSet<string>();

            foreach (var assignee in reminder.Card.Assignees)
                recipients.Add(assignee.Email!);

            recipients.Add(reminder.Card.CreatedBy.Email!);

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
            var recipients = new HashSet<string>();

            foreach (var assignee in card.Assignees)
            {
                if (assignee.AutomaticDeadlineReminder)
                    recipients.Add(assignee.Email!);
            }

            if (card.CreatedBy.AutomaticDeadlineReminder)
                recipients.Add(card.CreatedBy.Email!);

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
