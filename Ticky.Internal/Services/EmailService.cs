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
                    AddCodeKeys(new DevityTemplate(VERIFY_EMAIL), code)
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
                    AddCodeKeys(new DevityTemplate(FORGOTTEN_PASSWORD), code)
                )
            );

        /// <summary>The code and what the person needs to know about it, so the templates don't state it themselves.</summary>
        private static DevityTemplate AddCodeKeys(DevityTemplate template, Code code) =>
            template
                .AddKey(Constants.Emails.Mappings.VERIFICATION_CODE, code.Value)
                .AddKey(
                    Constants.Emails.Mappings.CODE_LIFETIME_IN_MINUTES,
                    Constants.Codes.LIFETIME_IN_MINUTES.ToString()
                )
                .AddKey(
                    Constants.Emails.Mappings.CODE_MAX_ATTEMPTS,
                    Constants.Codes.MAX_ATTEMPTS.ToString()
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
