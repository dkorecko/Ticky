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
    /// <summary>How long a code can be used for. The e-mail templates in wwwroot/emails mention the same time.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    /// <summary>How soon after a code was created another one can be requested for the same account.</summary>
    public static readonly TimeSpan ResendDelay = TimeSpan.FromMinutes(1);

    /// <summary>How many guesses a code allows. The code is deleted by the last wrong one.</summary>
    public const int MaxAttempts = 5;

    /// <summary>What the code input accepts: 8 letters or digits, the dash in the middle is optional.</summary>
    public const string InputPattern = @"\s*[A-Za-z0-9]{4}-?[A-Za-z0-9]{4}\s*";

    // No 0, 1, I, L or O, so a code can be read out and typed without mixing those up.
    private const string ALPHABET = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int GROUP_LENGTH = 4;

    private readonly DataContext _dataContext;
    private readonly CodeAttemptTracker _attempts;

    public CodeService(DataContext dataContext, CodeAttemptTracker attempts)
    {
        _dataContext = dataContext;
        _attempts = attempts;
    }

    /// <summary>
    /// Creates a code for the account. An account has a single code, so one that is still outstanding is replaced.
    /// </summary>
    public async Task<Code> CreateCodeAsync(User account, CodePurpose codePurpose)
    {
        var outstanding = await _dataContext
            .Codes.Where(x => x.UserId == account.Id)
            .Select(x => x.Id)
            .ToListAsync();

        if (outstanding.Count > 0)
        {
            foreach (
                var tracked in _dataContext
                    .ChangeTracker.Entries<Code>()
                    .Where(x => x.Entity.UserId == account.Id)
                    .ToList()
            )
                tracked.State = EntityState.Detached;

            await _dataContext.Codes.Where(x => x.UserId == account.Id).ExecuteDeleteAsync();

            foreach (var id in outstanding)
                _attempts.Forget(id);
        }

        var code = new Code
        {
            UserId = account.Id,
            CodePurpose = codePurpose,
            Value = GenerateCode(),
            CreatedAt = DateTime.Now
        };

        _dataContext.Codes.Add(code);
        await _dataContext.SaveChangesAsync();

        return code;
    }

    /// <summary>Whether the account got a code a moment ago, so another one shouldn't be mailed yet.</summary>
    public async Task<bool> HasRecentCodeAsync(User account)
    {
        var threshold = DateTime.Now - ResendDelay;
        return await _dataContext.Codes.AnyAsync(x =>
            x.UserId == account.Id && x.CreatedAt > threshold
        );
    }

    /// <summary>
    /// Checks the code submitted for the account with this e-mail address and returns it (with its user) when it is
    /// the account's current code for this purpose, hasn't expired and the guess is right. Every guess counts, and the
    /// code is deleted once it has expired or the last guess it allows was wrong. A code that is returned stays in
    /// the database until <see cref="ConsumeCodeAsync"/> is called, so a form that is sent back to the person for
    /// something else, like a weak password, doesn't cost them the code.
    /// </summary>
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

        if (code is null)
            return null;

        if (IsExpired(code))
        {
            await ConsumeCodeAsync(code);
            return null;
        }

        // The guess is counted before it is compared, so parallel requests can't make more guesses than allowed.
        var attempt = _attempts.Register(code.Id, Lifetime);

        if (attempt > MaxAttempts)
        {
            await ConsumeCodeAsync(code);
            return null;
        }

        if (Matches(code, submittedCode))
        {
            _attempts.Forget(code.Id);
            return code;
        }

        if (attempt >= MaxAttempts)
            await ConsumeCodeAsync(code);

        return null;
    }

    /// <summary>Deletes a code that was used. Returns whether this call was the one that deleted it.</summary>
    public async Task<bool> ConsumeCodeAsync(Code code)
    {
        var deleted = await _dataContext.Codes.Where(x => x.Id == code.Id).ExecuteDeleteAsync();
        _attempts.Forget(code.Id);

        return deleted > 0;
    }

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
