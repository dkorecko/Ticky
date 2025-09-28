using Devity.Extensions.Templates;
using Devity.Mailing;
using Devity.NETCore.MailKit.Core;

namespace Ticky.Internal.Services
{
    public class EmailService(IEmailService emailService, ILogger<EmailService> logger)
        : CommonMailService(emailService, $"{TITLE_KEY} | {Constants.APP_NAME}")
    {
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

        public async Task SendVerificationEmailAsync(string emailAddress, Code code) =>
            await SendDevityEmailAsync(
                new DevityEmail(
                    emailAddress,
                    "Verify e-mail",
                    new DevityTemplate(VERIFY_EMAIL).AddKey(
                        Constants.Emails.Mappings.VERIFICATION_CODE,
                        code.Value
                    )
                )
            );

        public async Task SendReminderEmailAsync(string emailAddress, Reminder reminder)
        {
            var cardCode = $"{reminder.Card.Column.Board.Code}-{reminder.Card.Number}";
            var cardPath = $"/boards/{reminder.Card.Column.BoardId}/{reminder.Card.Id}";

            await SendDevityEmailAsync(
                new DevityEmail(
                    emailAddress,
                    $"Reminder: {reminder.Card.Name} ({cardCode})",
                    new DevityTemplate(REMINDER)
                        .AddKey(Constants.Emails.Mappings.CARD_TEXT, reminder.Card.Name)
                        .AddKey(
                            Constants.Emails.Mappings.CARD_SCHEDULED_FOR,
                            reminder.At.ToReadableStringWithTime()
                        )
                        .AddKey(Constants.Emails.Mappings.CARD_CODE, cardCode)
                        .AddKey(
                            Constants.Emails.Mappings.CARD_DESCRIPTION,
                            reminder.Card.Description
                        )
                        .AddKey(
                            Constants.Emails.Mappings.CARD_URL,
                            $"{Constants.BASE_URL}{cardPath}"
                        )
                        .AddKey(
                            Constants.Emails.Mappings.CARD_DEADLINE,
                            reminder.Card.Deadline.GetValueOrDefault().ToReadableStringWithTime()
                        )
                        .AddCondition(
                            Constants.Emails.Mappings.CARD_DEADLINE_SECTION,
                            reminder.Card.Deadline.HasValue
                        )
                        .AddCondition(
                            Constants.Emails.Mappings.CARD_DESCRIPTION_SECTION,
                            !string.IsNullOrWhiteSpace(reminder.Card.Description)
                        )
                        .AddCondition(
                            Constants.Emails.Mappings.CARD_SUBTASKS_SECTION,
                            reminder.Card.Subtasks.Any()
                        )
                        .AddLoop(
                            Constants.Emails.Mappings.CARD_SUBTASKS,
                            new DevityTemplateLoop<Subtask>(reminder.Card.Subtasks)
                                .AddKey(Constants.Emails.Mappings.SUBTASK_TEXT, s => s.Text)
                                .AddKey(
                                    Constants.Emails.Mappings.SUBTASK_ICON,
                                    s => s.Completed ? "✓" : "○"
                                )
                                .AddKey(
                                    Constants.Emails.Mappings.SUBTASK_ICON_COLOR,
                                    s => s.Completed ? "#4CAF50" : "#F44336"
                                )
                                .AddKey(
                                    Constants.Emails.Mappings.SUBTASK_COMPLETED_CLASS,
                                    s => s.Completed ? " subtask-completed" : string.Empty
                                )
                        )
                )
            );
        }

        public async Task SendDeadlineReminderEmailAsync(string emailAddress, Card card)
        {
            var cardCode = $"{card.Column.Board.Code}-{card.Number}";
            var cardPath = $"/boards/{card.Column.BoardId}/{card.Id}";

            await SendDevityEmailAsync(
                new DevityEmail(
                    emailAddress,
                    $"Deadline today: {card.Name} {cardCode}",
                    new DevityTemplate(DEADLINE_REMINDER)
                        .AddKey(Constants.Emails.Mappings.CARD_TEXT, card.Name)
                        .AddKey(
                            Constants.Emails.Mappings.CARD_SCHEDULED_FOR,
                            card.Deadline!.Value.ToReadableStringWithTime()
                        )
                        .AddKey(Constants.Emails.Mappings.CARD_CODE, cardCode)
                        .AddKey(Constants.Emails.Mappings.CARD_DESCRIPTION, card.Description)
                        .AddKey(
                            Constants.Emails.Mappings.CARD_URL,
                            $"{Constants.BASE_URL}{cardPath}"
                        )
                        .AddCondition(
                            Constants.Emails.Mappings.CARD_DESCRIPTION_SECTION,
                            !string.IsNullOrWhiteSpace(card.Description)
                        )
                        .AddCondition(
                            Constants.Emails.Mappings.CARD_SUBTASKS_SECTION,
                            card.Subtasks.Any()
                        )
                        .AddLoop(
                            Constants.Emails.Mappings.CARD_SUBTASKS,
                            new DevityTemplateLoop<Subtask>(card.Subtasks)
                                .AddKey(Constants.Emails.Mappings.SUBTASK_TEXT, s => s.Text)
                                .AddKey(
                                    Constants.Emails.Mappings.SUBTASK_ICON,
                                    s => s.Completed ? "✓" : "○"
                                )
                                .AddKey(
                                    Constants.Emails.Mappings.SUBTASK_ICON_COLOR,
                                    s => s.Completed ? "#4CAF50" : "#F44336"
                                )
                                .AddKey(
                                    Constants.Emails.Mappings.SUBTASK_COMPLETED_CLASS,
                                    s => s.Completed ? " subtask-completed" : string.Empty
                                )
                        )
                )
            );
        }

        public async Task SendForgottenPasswordCodeEmailAsync(string emailAddress, Code code) =>
            await SendDevityEmailAsync(
                new DevityEmail(
                    emailAddress,
                    "Forgotten password",
                    new DevityTemplate(FORGOTTEN_PASSWORD).AddKey(
                        Constants.Emails.Mappings.VERIFICATION_CODE,
                        code.Value
                    )
                )
            );

        private async Task SendDevityEmailAsync(DevityEmail devityEmail)
        {
            if (!Constants.SMTP_ENABLED)
            {
                logger.LogInformation("SMTP disabled, could not send e-mail.");
                return;
            }

            await base.SendEmailAsync(devityEmail);
        }
    }
}
