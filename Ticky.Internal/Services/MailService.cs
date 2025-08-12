using NETCore.MailKit.Core;

namespace Ticky.Internal.Services
{
    public class MailService
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<MailService> _logger;
        private static readonly string VERIFY_EMAIL = Path.Combine(
            Constants.Emails.BASE_PATH,
            "VerifyEmail.html"
        );
        private static readonly string FORGOTTEN_PASSWORD = Path.Combine(
            Constants.Emails.BASE_PATH,
            "ForgottenPassword.html"
        );
        private static readonly string REMINDER = Path.Combine(
            Constants.Emails.BASE_PATH,
            "Reminder.html"
        );
        private static readonly string DEADLINE_REMINDER = Path.Combine(
            Constants.Emails.BASE_PATH,
            "DeadlineReminder.html"
        );

        public MailService(IEmailService emailService, ILogger<MailService> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string emailAddress, Code code) =>
            await SendEmailAsync(
                emailAddress,
                "Verify e-mail",
                VERIFY_EMAIL,
                new Dictionary<string, string>
                {
                    { Constants.Emails.Mappings.VERIFICATION_CODE, code.Value }
                }
            );

        public async Task SendReminderEmailAsync(string emailAddress, Reminder reminder)
        {
            var cardCode = $"{reminder.Card.Column.Board.Code}-{reminder.Card.Number}";
            var cardPath = $"/boards/{reminder.Card.Column.BoardId}/{reminder.Card.Id}";

            await SendEmailAsync(
                emailAddress,
                $"Reminder: {reminder.Card.Name} ({cardCode})",
                REMINDER,
                new Dictionary<string, string>
                {
                    { Constants.Emails.Mappings.CARD_TEXT, reminder.Card.Name },
                    {
                        Constants.Emails.Mappings.CARD_SCHEDULED_FOR,
                        reminder.At.ToReadableStringWithTime()
                    },
                    { Constants.Emails.Mappings.CARD_CODE, cardCode },
                    {
                        Constants.Emails.Mappings.CARD_DESCRIPTION,
                        string.IsNullOrWhiteSpace(reminder.Card.Description)
                            ? "No description set."
                            : reminder.Card.Description
                    },
                    {
                        Constants.Emails.Mappings.CARD_SUBTASKS,
                        GenerateSubtasksHtml(reminder.Card.Subtasks)
                    },
                    {
                        Constants.Emails.Mappings.CARD_URL,
                        $"{Constants.BASE_URL.TrimEnd('/')}{cardPath}"
                    },
                    {
                        Constants.Emails.Mappings.CARD_DEADLINE,
                        reminder.Card.Deadline.HasValue
                            ? reminder.Card.Deadline.Value.ToReadableStringWithTime()
                            : "No deadline set."
                    }
                }
            );
        }

        public async Task SendDeadlineReminderEmailAsync(string emailAddress, Card card) =>
            await SendEmailAsync(
                emailAddress,
                $"Deadline today for task {card.Column.Board.Code}-{card.Number}",
                DEADLINE_REMINDER,
                new Dictionary<string, string>
                {
                    { Constants.Emails.Mappings.CARD_TEXT, card.Name },
                    {
                        Constants.Emails.Mappings.CARD_SCHEDULED_FOR,
                        card!.Deadline!.Value.ToReadableStringWithTime()
                    },
                    {
                        Constants.Emails.Mappings.CARD_CODE,
                        $"{card.Column.Board.Code}-{card.Number}"
                    },
                }
            );

        public async Task SendForgottenPasswordCodeEmailAsync(string emailAddress, Code code) =>
            await SendEmailAsync(
                emailAddress,
                "Forgotten password",
                FORGOTTEN_PASSWORD,
                new Dictionary<string, string>
                {
                    { Constants.Emails.Mappings.VERIFICATION_CODE, code.Value }
                }
            );

        private string GenerateSubtasksHtml(List<Subtask> subtasks)
        {
            if (!subtasks.Any())
                return "<p style=\"color: #9ca3af; font-style: italic; margin: 0;\">No subtasks</p>";

            var html = "<div class=\"subtasks-list\">";
            var subtaskList = subtasks.OrderBy(s => s.Index).ToList();

            for (int i = 0; i < subtaskList.Count; i++)
            {
                var subtask = subtaskList[i];
                var icon = subtask.Completed ? "✓" : "○";
                var completedClass = subtask.Completed ? " subtask-completed" : "";

                html +=
                    $"<div class=\"subtask-item{completedClass}\" style=\"display: flex; align-items: center; padding: 10px 0; border-bottom: 1px solid #e5e7eb;\">";
                html +=
                    $"<span style=\"display: inline-flex; align-items: center; justify-content: center; width: 20px; height: 20px; font-size: 14px; line-height: 1; flex-shrink: 0; margin-right: 4px; color: {(subtask.Completed ? "#10b981" : "#6b7280")}; font-weight: bold;\">{icon}</span>";
                html += $"<span>{subtask.Text}</span>";
                html += "</div>";
            }
            html += "</div>";
            return html;
        }

        private async Task SendEmailAsync(
            string emailAddress,
            string subject,
            string bodyPath,
            Dictionary<string, string>? data = null
        )
        {
            if (!Constants.SMTP_ENABLED)
            {
                _logger.LogWarning("SMTP disabled, could not send e-mail.");
                return;
            }

            if (!File.Exists(bodyPath))
                throw new FileNotFoundException(
                    "Could not find e-mail body file when sending e-mail."
                );

            string body = File.ReadAllText(bodyPath);

            if (data is not null)
                foreach (var dataPoint in data)
                    body = body.Replace(dataPoint.Key, dataPoint.Value);

            await _emailService.SendAsync(emailAddress, $"{subject} | Ticky", body, true);
        }
    }
}
