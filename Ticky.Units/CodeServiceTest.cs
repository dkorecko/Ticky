using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticky.Internal.Data;
using AppConstants = Ticky.Base.Constants;

namespace Ticky.Units;

/// <summary>Runs CodeService against an in-memory SQLite database.</summary>
public class CodeServiceTest
{
    private const string ALICE = "alice@test.com";
    private const string BOB = "bob@test.com";
    private const string CAROL = "carol@test.com";

    private SqliteConnection _connection = null!;
    private DataContext _db = null!;
    private CodeService _service = null!;

    private int _aliceId;
    private int _bobId;
    private int _carolId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _db = new DataContext(
            new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options
        );
        _db.Database.EnsureCreated();

        _service = new CodeService(_db);

        _aliceId = await CreateUserAsync(ALICE);
        _bobId = await CreateUserAsync(BOB);
        // Signed up, but hasn't confirmed the e-mail address yet.
        _carolId = await CreateUserAsync(CAROL, confirmed: false);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // Generating codes

    [Test]
    public void GenerateCode_UsesTheFormatAndEveryCharacterOfTheAlphabet()
    {
        var codes = Enumerable.Range(0, 3000).Select(_ => CodeService.GenerateCode()).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(codes, Has.All.Match("^[2-9A-HJKMNP-Z]{4}-[2-9A-HJKMNP-Z]{4}$"));
            Assert.That(codes, Is.Unique);
            // The old generator never produced the last character of its alphabet.
            Assert.That(codes.SelectMany(x => x).Where(x => x != '-').Distinct().Count(), Is.EqualTo(31));
        });
    }

    [Test]
    public void GenerateCode_FitsTheInputPatternOfTheForms()
    {
        // The attribute on the forms needs the whole value to match.
        bool Accepts(string input) => System.Text.RegularExpressions.Regex.IsMatch(input, $"^(?:{AppConstants.Codes.INPUT_PATTERN})$");

        Assert.Multiple(() =>
        {
            Assert.That(Accepts(CodeService.GenerateCode()), Is.True);
            Assert.That(Accepts("k7m2-qx4p"), Is.True);
            Assert.That(Accepts(" K7M2QX4P "), Is.True);
            Assert.That(Accepts("123-456"), Is.False);
            Assert.That(Accepts("K7M2-QX4P-9"), Is.False);
        });
    }

    // Matching

    [Test]
    public async Task VerifyCode_RightEmailAndCode_ReturnsTheCodeWithItsUser()
    {
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        var verified = await _service.VerifyCodeAsync(ALICE, code.Value, CodePurpose.ForgottenPassword);

        Assert.Multiple(() =>
        {
            Assert.That(verified, Is.Not.Null);
            Assert.That(verified!.User.Id, Is.EqualTo(_aliceId));
        });
    }

    [Test]
    public async Task VerifyCode_IgnoresCaseDashAndSpaces()
    {
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);
        var sloppy = $"  {code.Value.Replace("-", string.Empty).ToLowerInvariant()} ";

        Assert.That(
            await _service.VerifyCodeAsync($" {ALICE} ", sloppy, CodePurpose.ForgottenPassword),
            Is.Not.Null
        );
    }

    [Test]
    public async Task VerifyCode_CodeOfAnotherAccount_Fails()
    {
        var alicesCode = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);
        await _service.CreateCodeAsync(await GetUserAsync(_bobId), CodePurpose.ForgottenPassword);

        var withBobsEmail = await _service.VerifyCodeAsync(BOB, alicesCode.Value, CodePurpose.ForgottenPassword);
        var withAlicesEmail = await _service.VerifyCodeAsync(ALICE, alicesCode.Value, CodePurpose.ForgottenPassword);

        Assert.Multiple(() =>
        {
            Assert.That(withBobsEmail, Is.Null);
            Assert.That(withAlicesEmail, Is.Not.Null);
        });
    }

    [Test]
    public async Task VerifyCode_CodeWithoutEmailOfAnyAccount_Fails()
    {
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _service.VerifyCodeAsync("nobody@test.com", code.Value, CodePurpose.ForgottenPassword), Is.Null);
            Assert.That(await _service.VerifyCodeAsync(string.Empty, code.Value, CodePurpose.ForgottenPassword), Is.Null);
        });
    }

    [Test]
    public async Task VerifyCode_OtherPurpose_Fails()
    {
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.NewAccount);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _service.VerifyCodeAsync(ALICE, code.Value, CodePurpose.ForgottenPassword), Is.Null);
            // Asking for it with the wrong purpose neither uses it up nor counts as a guess.
            Assert.That(await _db.Codes.CountAsync(x => x.UserId == _aliceId), Is.EqualTo(1));
            Assert.That(await _service.VerifyCodeAsync(ALICE, code.Value, CodePurpose.NewAccount), Is.Not.Null);
        });
    }

    // Expiry

    [Test]
    public async Task VerifyCode_AfterTheLifetime_FailsAndDeletesTheCode()
    {
        await AddCodeAsync(_aliceId, CodePurpose.ForgottenPassword, "ABCD-2345", DateTime.Now - CodeService.Lifetime - TimeSpan.FromMinutes(1));

        var verified = await _service.VerifyCodeAsync(ALICE, "ABCD-2345", CodePurpose.ForgottenPassword);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(verified, Is.Null);
            Assert.That(await _db.Codes.CountAsync(x => x.UserId == _aliceId), Is.Zero);
        });
    }

    [Test]
    public async Task VerifyCode_JustBeforeTheLifetimeEnds_Works()
    {
        await AddCodeAsync(_aliceId, CodePurpose.ForgottenPassword, "ABCD-2345", DateTime.Now - CodeService.Lifetime + TimeSpan.FromMinutes(1));

        Assert.That(await _service.VerifyCodeAsync(ALICE, "ABCD-2345", CodePurpose.ForgottenPassword), Is.Not.Null);
    }

    [Test]
    public void IsExpired_UsesTheLifetime()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CodeService.IsExpired(NewCode(DateTime.Now)), Is.False);
            Assert.That(CodeService.IsExpired(NewCode(DateTime.Now - TimeSpan.FromMinutes(29))), Is.False);
            Assert.That(CodeService.IsExpired(NewCode(DateTime.Now - TimeSpan.FromMinutes(31))), Is.True);
        });
    }

    // Guess limit

    [Test]
    public async Task VerifyCode_AfterTheLastAllowedWrongGuess_DeletesTheCode()
    {
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        for (var i = 0; i < AppConstants.Codes.MAX_ATTEMPTS; i++)
            Assert.That(await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword), Is.Null);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _db.Codes.CountAsync(x => x.UserId == _aliceId), Is.Zero);
            // Even the right code is gone, a new one has to be requested.
            Assert.That(await _service.VerifyCodeAsync(ALICE, code.Value, CodePurpose.ForgottenPassword), Is.Null);
        });
    }

    [Test]
    public async Task VerifyCode_RightCodeWithinTheGuessLimit_Works()
    {
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        for (var i = 0; i < AppConstants.Codes.MAX_ATTEMPTS - 1; i++)
            await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword);

        Assert.That(await _service.VerifyCodeAsync(ALICE, code.Value, CodePurpose.ForgottenPassword), Is.Not.Null);
    }

    [Test]
    public async Task VerifyCode_RightCodeResetsTheGuessCount()
    {
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        for (var round = 0; round < 3; round++)
        {
            for (var i = 0; i < AppConstants.Codes.MAX_ATTEMPTS - 1; i++)
                await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword);

            // Like a person whose new password was refused: the code stays and may be used again.
            Assert.That(await _service.VerifyCodeAsync(ALICE, code.Value, CodePurpose.ForgottenPassword), Is.Not.Null);
        }
    }

    [Test]
    public async Task VerifyCode_GuessesAtOneAccount_DontAffectAnother()
    {
        var alicesCode = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);
        await _service.CreateCodeAsync(await GetUserAsync(_bobId), CodePurpose.ForgottenPassword);

        for (var i = 0; i < AppConstants.Codes.MAX_ATTEMPTS * 2; i++)
            await _service.VerifyCodeAsync(BOB, alicesCode.Value, CodePurpose.ForgottenPassword);

        await Assert.MultipleAsync(async () =>
        {
            // Bob's own code was used up by the guesses at his e-mail address...
            Assert.That(await _db.Codes.CountAsync(x => x.UserId == _bobId), Is.Zero);
            // ...and Alice's wasn't touched.
            Assert.That(await _service.VerifyCodeAsync(ALICE, alicesCode.Value, CodePurpose.ForgottenPassword), Is.Not.Null);
        });
    }

    [Test]
    public async Task VerifyCode_NewCodeStartsWithoutTheGuessesOfTheOldOne()
    {
        var alice = await GetUserAsync(_aliceId);
        await _service.CreateCodeAsync(alice, CodePurpose.ForgottenPassword);

        for (var i = 0; i < AppConstants.Codes.MAX_ATTEMPTS - 1; i++)
            await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword);

        var replacement = await _service.CreateCodeAsync(alice, CodePurpose.ForgottenPassword);

        for (var i = 0; i < AppConstants.Codes.MAX_ATTEMPTS - 1; i++)
            await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword);

        Assert.That(await _service.VerifyCodeAsync(ALICE, replacement.Value, CodePurpose.ForgottenPassword), Is.Not.Null);
    }

    // Creating and using up codes

    [Test]
    public async Task CreateCode_ReplacesTheOutstandingCodeOfTheAccount()
    {
        var alice = await GetUserAsync(_aliceId);
        var first = await _service.CreateCodeAsync(alice, CodePurpose.ForgottenPassword);
        var second = await _service.CreateCodeAsync(alice, CodePurpose.ForgottenPassword);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _db.Codes.CountAsync(x => x.UserId == _aliceId), Is.EqualTo(1));
            Assert.That(second.Value, Is.Not.EqualTo(first.Value));
            Assert.That(await _service.VerifyCodeAsync(ALICE, first.Value, CodePurpose.ForgottenPassword), Is.Null);
        });
    }

    [Test]
    public async Task CreateCode_AccountThatHasItsCodeLoaded_ReplacesIt()
    {
        await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.NewAccount);
        var alice = await _db.Users.Include(x => x.EmailVerificationCode).SingleAsync(x => x.Id == _aliceId);

        var replacement = await _service.CreateCodeAsync(alice, CodePurpose.ForgottenPassword);

        var stored = await _db.Codes.AsNoTracking().SingleAsync(x => x.UserId == _aliceId);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Value, Is.EqualTo(replacement.Value));
            Assert.That(stored.CodePurpose, Is.EqualTo(CodePurpose.ForgottenPassword));
        });
    }

    [Test]
    public async Task CreateCode_PendingSignUp_KeepsItsCodeAndRefusesAnother()
    {
        var carol = await GetUserAsync(_carolId);
        var signUp = await _service.CreateCodeAsync(carol, CodePurpose.NewAccount);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.CreateCodeAsync(carol, CodePurpose.ForgottenPassword)
        );

        var stored = await _db.Codes.AsNoTracking().SingleAsync(x => x.UserId == _carolId);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Value, Is.EqualTo(signUp.Value));
            Assert.That(stored.CodePurpose, Is.EqualTo(CodePurpose.NewAccount));
        });
    }

    [Test]
    public async Task CreateCode_PendingSignUpAskingForANewSignUpCode_ReplacesIt()
    {
        var carol = await GetUserAsync(_carolId);
        var first = await _service.CreateCodeAsync(carol, CodePurpose.NewAccount);

        var second = await _service.CreateCodeAsync(carol, CodePurpose.NewAccount);

        Assert.Multiple(() =>
        {
            Assert.That(second.Value, Is.Not.EqualTo(first.Value));
            Assert.That(_db.Codes.Count(x => x.UserId == _carolId), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task HasPendingSignUp_OnlyForAnUnconfirmedAccountWithASignUpCode()
    {
        var alice = await GetUserAsync(_aliceId);
        var bob = await GetUserAsync(_bobId);
        var carol = await GetUserAsync(_carolId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _service.HasPendingSignUpAsync(carol), Is.False, "no code yet");

            // Whatever the age of the code: the account stays pending until the cleanup removes both.
            await AddCodeAsync(_carolId, CodePurpose.NewAccount, "ABCD-2345", DateTime.Now - CodeService.Lifetime - TimeSpan.FromHours(1));
            Assert.That(await _service.HasPendingSignUpAsync(carol), Is.True);

            await AddCodeAsync(_aliceId, CodePurpose.NewAccount, "ABCD-2345", DateTime.Now);
            Assert.That(await _service.HasPendingSignUpAsync(alice), Is.False, "confirmed account");

            await AddCodeAsync(_bobId, CodePurpose.ForgottenPassword, "EFGH-2345", DateTime.Now);
            Assert.That(await _service.HasPendingSignUpAsync(bob), Is.False, "reset code");
        });
    }

    [Test]
    public async Task CreateCode_ConfirmedAccountWithAnOldSignUpCode_ReplacesIt()
    {
        await AddCodeAsync(_aliceId, CodePurpose.NewAccount, "ABCD-2345", DateTime.Now);

        var replacement = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        var stored = await _db.Codes.AsNoTracking().SingleAsync(x => x.UserId == _aliceId);
        Assert.That(stored.Value, Is.EqualTo(replacement.Value));
    }

    [Test]
    public async Task ConsumeCode_DeletesTheCodeOnlyOnce()
    {
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _service.ConsumeCodeAsync(code), Is.True);
            Assert.That(await _service.ConsumeCodeAsync(code), Is.False);
            Assert.That(await _service.VerifyCodeAsync(ALICE, code.Value, CodePurpose.ForgottenPassword), Is.Null);
        });
    }

    [Test]
    public async Task HasRecentCode_OnlyRightAfterACodeWasCreated()
    {
        var alice = await GetUserAsync(_aliceId);
        var bob = await GetUserAsync(_bobId);
        await _service.CreateCodeAsync(alice, CodePurpose.ForgottenPassword);
        await AddCodeAsync(_bobId, CodePurpose.ForgottenPassword, "ABCD-2345", DateTime.Now - CodeService.ResendDelay - TimeSpan.FromSeconds(5));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _service.HasRecentCodeAsync(alice), Is.True);
            Assert.That(await _service.HasRecentCodeAsync(bob), Is.False);
        });
    }

    // Counting guesses

    [Test]
    public async Task VerifyCode_WrongGuesses_AreCountedOnTheCode()
    {
        await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword);
        await _service.VerifyCodeAsync(ALICE, "WRONG-24", CodePurpose.ForgottenPassword);

        Assert.That(await GetFailedAttemptsAsync(_aliceId), Is.EqualTo(2));
    }

    [Test]
    public async Task VerifyCode_RightGuess_ResetsTheStoredCount()
    {
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        for (var i = 0; i < AppConstants.Codes.MAX_ATTEMPTS - 1; i++)
            await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword);

        await _service.VerifyCodeAsync(ALICE, code.Value, CodePurpose.ForgottenPassword);

        Assert.That(await GetFailedAttemptsAsync(_aliceId), Is.Zero);
    }

    [Test]
    public async Task VerifyCode_CountSurvivesANewServiceAndContext()
    {
        // The count is stored with the code, so it doesn't depend on the instance (or the process) that counted.
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        for (var i = 0; i < AppConstants.Codes.MAX_ATTEMPTS - 1; i++)
            await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword);

        using var otherDb = new DataContext(
            new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options
        );
        var otherService = new CodeService(otherDb);

        Assert.Multiple(async () =>
        {
            Assert.That(await otherService.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword), Is.Null);
            Assert.That(await _db.Codes.CountAsync(x => x.UserId == _aliceId), Is.Zero);
            Assert.That(await otherService.VerifyCodeAsync(ALICE, code.Value, CodePurpose.ForgottenPassword), Is.Null);
        });
    }

    [Test]
    public async Task VerifyCode_CodeThatReachedTheLimit_IsRefusedEvenWithTheRightGuess()
    {
        await AddCodeAsync(_aliceId, CodePurpose.ForgottenPassword, "ABCD-2345", DateTime.Now, AppConstants.Codes.MAX_ATTEMPTS);

        var verified = await _service.VerifyCodeAsync(ALICE, "ABCD-2345", CodePurpose.ForgottenPassword);

        Assert.Multiple(() =>
        {
            Assert.That(verified, Is.Null);
            Assert.That(_db.Codes.Count(x => x.UserId == _aliceId), Is.Zero);
        });
    }

    [Test]
    public async Task CreateCode_StartsWithoutFailedAttempts()
    {
        var alice = await GetUserAsync(_aliceId);
        await _service.CreateCodeAsync(alice, CodePurpose.ForgottenPassword);
        await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword);

        await _service.CreateCodeAsync(alice, CodePurpose.ForgottenPassword);

        Assert.That(await GetFailedAttemptsAsync(_aliceId), Is.Zero);
    }

    private async Task<int> CreateUserAsync(string email, bool confirmed = true)
    {
        var user = new User
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = email,
            EmailConfirmed = confirmed,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return user.Id;
    }

    private async Task<User> GetUserAsync(int id) => await _db.Users.SingleAsync(x => x.Id == id);

    private async Task<int> GetFailedAttemptsAsync(int userId) =>
        await _db.Codes.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.FailedAttempts).SingleAsync();

    private async Task AddCodeAsync(int userId, CodePurpose purpose, string value, DateTime createdAt, int failedAttempts = 0)
    {
        _db.Codes.Add(
            new Code
            {
                UserId = userId,
                CodePurpose = purpose,
                Value = value,
                CreatedAt = createdAt,
                FailedAttempts = failedAttempts
            }
        );
        await _db.SaveChangesAsync();
    }

    private static Code NewCode(DateTime createdAt) =>
        new()
        {
            UserId = 1,
            CodePurpose = CodePurpose.ForgottenPassword,
            Value = "ABCD-2345",
            CreatedAt = createdAt
        };
}
