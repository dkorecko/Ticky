using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Ticky.Internal.Services;

/// <summary>
/// Creates and checks the codes mailed to people to confirm an e-mail address or to reset a password. A code is only
/// accepted together with the e-mail address of its account and for the purpose it was created for, for a limited
/// time and for a limited number of guesses.
/// </summary>
public class CodeService
{
    /// <summary>How long a code can be used for, see <see cref="Constants.Codes.LIFETIME_IN_MINUTES"/>.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(Constants.Codes.LIFETIME_IN_MINUTES);

    /// <summary>How soon another code can be requested, see <see cref="Constants.Codes.RESEND_DELAY_IN_SECONDS"/>.</summary>
    public static readonly TimeSpan ResendDelay = TimeSpan.FromSeconds(Constants.Codes.RESEND_DELAY_IN_SECONDS);

    // No 0, 1, I, L or O, so a code can be read out and typed without mixing those up.
    private const string ALPHABET = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int GROUP_LENGTH = 4;

    private readonly DataContext _dataContext;

    public CodeService(DataContext dataContext)
    {
        _dataContext = dataContext;
    }

    /// <summary>
    /// Creates a code for the account, replacing the one that is outstanding (an account has a single code), except
    /// the sign-up code of an account that isn't confirmed yet: it marks the account as pending, so nothing else may
    /// take its place.
    /// </summary>
    /// <exception cref="InvalidOperationException">Another code was asked for a pending sign-up.</exception>
    public async Task<Code> CreateCodeAsync(User account, CodePurpose codePurpose) =>
        (await IssueCodeAsync(account, codePurpose, keepRecent: false))!;

    /// <summary>
    /// Like <see cref="CreateCodeAsync"/>, but a code that was created less than <see cref="ResendDelay"/> ago is left
    /// alone (even if it is used up), so asking again right away neither replaces the code that was just mailed nor
    /// mails another one. Only codes older than that are deleted, and the unique index on the account decides between
    /// requests that arrive together, so exactly one of them creates the code.
    /// </summary>
    /// <returns>The new code, or <c>null</c> when a recent one is in place and nothing should be mailed.</returns>
    /// <exception cref="InvalidOperationException">Another code was asked for a pending sign-up.</exception>
    public async Task<Code?> TryCreateCodeAsync(User account, CodePurpose codePurpose) =>
        await IssueCodeAsync(account, codePurpose, keepRecent: true);

    private async Task<Code?> IssueCodeAsync(User account, CodePurpose codePurpose, bool keepRecent)
    {
        if (codePurpose != CodePurpose.NewAccount && await HasPendingSignUpAsync(account))
            throw new InvalidOperationException(
                "The account is waiting for its e-mail address to be confirmed, so its code can't be replaced."
            );

        foreach (
            var tracked in _dataContext
                .ChangeTracker.Entries<Code>()
                .Where(x => x.Entity.UserId == account.Id)
                .ToList()
        )
            tracked.State = EntityState.Detached;

        var outstanding = _dataContext.Codes.Where(x => x.UserId == account.Id);

        if (keepRecent)
        {
            var threshold = DateTime.Now - ResendDelay;

            // The usual case. Looking first keeps the unique index, which still decides between requests that arrive
            // together, from being hit (and logged as an error by EF) every time a code was just created.
            if (await _dataContext.Codes.AnyAsync(x => x.UserId == account.Id && x.CreatedAt > threshold))
                return null;

            outstanding = outstanding.Where(x => x.CreatedAt <= threshold);
        }

        await outstanding.ExecuteDeleteAsync();

        var code = new Code
        {
            UserId = account.Id,
            CodePurpose = codePurpose,
            Value = GenerateCode(),
            CreatedAt = DateTime.Now
        };

        _dataContext.Codes.Add(code);

        try
        {
            await _dataContext.SaveChangesAsync();
        }
        catch (DbUpdateException) when (keepRecent)
        {
            _dataContext.Entry(code).State = EntityState.Detached;

            // The recent code (or the one a parallel request has just created) is in place.
            if (await _dataContext.Codes.AnyAsync(x => x.UserId == account.Id))
                return null;

            throw;
        }

        return code;
    }

    /// <summary>Whether the account is a sign-up that hasn't confirmed its e-mail address yet, whatever the code's age.</summary>
    public async Task<bool> HasPendingSignUpAsync(User account) =>
        await _dataContext.Codes.AnyAsync(x =>
            x.UserId == account.Id && x.CodePurpose == CodePurpose.NewAccount && !x.User.EmailConfirmed
        );

    /// <summary>
    /// Checks the code submitted for the account with this e-mail address and returns it (with its user) when it is
    /// the account's current code for this purpose, hasn't expired and the guess is right. Every guess counts, and
    /// the guesses a code allows are limited. A code that is returned stays in the database until
    /// <see cref="ConsumeCodeAsync"/> is called, so a form that is sent back to the person for something else, like
    /// a weak password, doesn't cost them the code.
    /// </summary>
    /// <remarks>
    /// A code that has expired or is used up isn't deleted here, it just stops working. Its row keeps a pending
    /// sign-up marked (see <see cref="HasPendingSignUpAsync"/>) and keeps the resend delay running, and the cleanup
    /// job removes it, together with the account of a sign-up that was never confirmed.
    /// </remarks>
    /// <returns>
    /// The code, or <c>null</c> without a reason, so the caller can't tell an unknown e-mail address from a wrong,
    /// expired or used up code.
    /// </returns>
    public async Task<Code?> VerifyCodeAsync(
        string emailAddress,
        string submittedCode,
        CodePurpose purpose
    )
    {
        var email = emailAddress.Trim();
        var code = await _dataContext
            .Codes.Include(x => x.User)
            .FirstOrDefaultAsync(x => x.User.Email == email && x.CodePurpose == purpose);

        if (code is null || IsExpired(code))
            return null;

        // The guess is counted in the database before it is compared, and only while the limit isn't reached, so
        // requests made in parallel (or to several instances) can't make more guesses than allowed.
        var counted = await _dataContext
            .Codes.Where(x => x.Id == code.Id && x.FailedAttempts < Constants.Codes.MAX_ATTEMPTS)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.FailedAttempts, y => y.FailedAttempts + 1));

        if (counted == 0)
            return null;

        if (!Matches(code, submittedCode))
            return null;

        // Like a person whose new password was refused: the code stays and may be used again.
        await _dataContext
            .Codes.Where(x => x.Id == code.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.FailedAttempts, 0));

        return code;
    }

    /// <summary>Deletes a code that was used. Returns whether this call was the one that deleted it.</summary>
    public async Task<bool> ConsumeCodeAsync(Code code) =>
        await _dataContext.Codes.Where(x => x.Id == code.Id).ExecuteDeleteAsync() > 0;

    /// <summary>
    /// The codes the cleanup job removes: those past their lifetime, and sign-up codes of accounts that have been
    /// confirmed in the meantime. A password reset code of a confirmed account is a live one until it expires.
    /// </summary>
    public static Expression<Func<Code, bool>> Obsolete(DateTime expiredBefore) =>
        x =>
            expiredBefore > x.CreatedAt
            || (x.CodePurpose == CodePurpose.NewAccount && x.User != null && x.User.EmailConfirmed);

    public static bool IsExpired(Code code) => DateTime.Now - code.CreatedAt > Lifetime;

    /// <summary>Generates a code like <c>K7M2-QX4P</c>, about 40 bits of it, from a cryptographic random source.</summary>
    public static string GenerateCode()
    {
        Span<char> code = stackalloc char[(GROUP_LENGTH * 2) + 1];

        for (var i = 0; i < code.Length; i++)
            code[i] = i == GROUP_LENGTH ? '-' : ALPHABET[RandomNumberGenerator.GetInt32(ALPHABET.Length)];

        return new string(code);
    }

    /// <summary>Compares two codes ignoring case, dashes and spaces.</summary>
    public static bool Matches(Code code, string submittedCode) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Normalize(code.Value)),
            Encoding.UTF8.GetBytes(Normalize(submittedCode))
        );

    private static string Normalize(string code) =>
        new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
