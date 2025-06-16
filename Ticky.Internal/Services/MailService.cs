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

        public async Task SendReminderEmailAsync(string emailAddress, Reminder reminder) =>
            await SendEmailAsync(
                emailAddress,
                $"Reminder for task {reminder.Card.Column.Board.Code}-{reminder.Card.Number}",
                REMINDER,
                new Dictionary<string, string>
                {
                    { Constants.Emails.Mappings.CARD_TEXT, reminder.Card.Name },
                    {
                        Constants.Emails.Mappings.CARD_SCHEDULED_FOR,
                        reminder.At.ToReadableStringWithTime()
                    },
                    {
                        Constants.Emails.Mappings.CARD_CODE,
                        $"{reminder.Card.Column.Board.Code}-{reminder.Card.Number}"
                    },
                }
            );

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
