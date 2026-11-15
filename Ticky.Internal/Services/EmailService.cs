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

        public async Task SendReminderEmailAsync(string emailAddress, Reminder reminder) =>
            await SendDevityEmailAsync(
                new DevityEmail(
                    emailAddress,
                    EmailTemplateHelper.ToSubject(
                        $"Reminder: {reminder.Card.Name} ({EmailTemplateHelper.GetCardCode(reminder.Card)})"
                    ),
                    EmailTemplateHelper.BuildReminderTemplate(reminder)
                )
            );

        public async Task SendDeadlineReminderEmailAsync(string emailAddress, Card card) =>
            await SendDevityEmailAsync(
                new DevityEmail(
                    emailAddress,
                    EmailTemplateHelper.ToSubject(
                        $"Deadline today: {card.Name} {EmailTemplateHelper.GetCardCode(card)}"
                    ),
                    EmailTemplateHelper.BuildDeadlineReminderTemplate(card)
                )
            );

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
