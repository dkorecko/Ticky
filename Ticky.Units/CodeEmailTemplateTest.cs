using System.Reflection;
using System.Text.RegularExpressions;
using AppConstants = Ticky.Base.Constants;

namespace Ticky.Units;

/// <summary>
/// The e-mails with a code get its lifetime and guess limit from <see cref="AppConstants.Codes"/> through
/// placeholders, so they can't promise other numbers than the ones that are enforced.
/// </summary>
public class CodeEmailTemplateTest
{
    [TestCase("VerifyEmail.html")]
    [TestCase("ForgottenPassword.html")]
    public void Template_UsesThePlaceholdersForTheCodeAndItsLimits(string name)
    {
        var html = ReadTemplate(name);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain(AppConstants.Emails.Mappings.VERIFICATION_CODE));
            Assert.That(html, Does.Contain(AppConstants.Emails.Mappings.CODE_LIFETIME_IN_MINUTES));
            Assert.That(html, Does.Contain(AppConstants.Emails.Mappings.CODE_MAX_ATTEMPTS));
        });
    }

    [TestCase("VerifyEmail.html")]
    [TestCase("ForgottenPassword.html")]
    public void Template_StatesNoNumbersOfItsOwn(string name)
    {
        Assert.That(Regex.IsMatch(ReadTemplate(name), @"\d+\s+(minutes?|hours?|wrong|attempts?|tries)"), Is.False);
    }

    [TestCase("VerifyEmail.html")]
    [TestCase("ForgottenPassword.html")]
    public void Template_OnlyUsesKnownPlaceholders(string name)
    {
        var known = typeof(AppConstants.Emails.Mappings)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.IsLiteral)
            .Select(x => (string?)x.GetRawConstantValue())
            .ToHashSet();

        var used = Regex.Matches(ReadTemplate(name), @"\{[A-Z][A-Z_]*\}").Select(x => x.Value).Distinct();

        Assert.That(used, Is.SubsetOf(known));
    }

    [Test]
    public void Lifetime_AndLimitInTheConstants_AreWhatTheServiceEnforces()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CodeService.Lifetime, Is.EqualTo(TimeSpan.FromMinutes(AppConstants.Codes.LIFETIME_IN_MINUTES)));
            Assert.That(CodeService.ResendDelay, Is.EqualTo(TimeSpan.FromSeconds(AppConstants.Codes.RESEND_DELAY_IN_SECONDS)));
        });
    }

    private static string ReadTemplate(string name)
    {
        // The test assembly runs from Ticky.Units/bin/..., the templates are the ones the web project serves.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "Ticky.Web", "wwwroot", "emails", name);

            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new FileNotFoundException($"Could not find the e-mail template {name} above {AppContext.BaseDirectory}.");
    }
}
