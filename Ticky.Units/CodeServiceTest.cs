using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticky.Internal.Data;

namespace Ticky.Units;

/// <summary>Runs CodeService against an in-memory SQLite database.</summary>
public class CodeServiceTest
{
    private const string ALICE = "alice@test.com";
    private const string BOB = "bob@test.com";

    private SqliteConnection _connection = null!;
    private DataContext _db = null!;
    private CodeService _service = null!;

    private int _aliceId;
    private int _bobId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _db = new DataContext(
            new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options
        );
        _db.Database.EnsureCreated();

        _service = new CodeService(_db, new CodeAttemptTracker());

        _aliceId = await CreateUserAsync(ALICE);
        _bobId = await CreateUserAsync(BOB);
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
        bool Accepts(string input) => System.Text.RegularExpressions.Regex.IsMatch(input, $"^(?:{CodeService.InputPattern})$");

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

        for (var i = 0; i < CodeService.MaxAttempts; i++)
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

        for (var i = 0; i < CodeService.MaxAttempts - 1; i++)
            await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword);

        Assert.That(await _service.VerifyCodeAsync(ALICE, code.Value, CodePurpose.ForgottenPassword), Is.Not.Null);
    }

    [Test]
    public async Task VerifyCode_RightCodeResetsTheGuessCount()
    {
        var code = await _service.CreateCodeAsync(await GetUserAsync(_aliceId), CodePurpose.ForgottenPassword);

        for (var round = 0; round < 3; round++)
        {
            for (var i = 0; i < CodeService.MaxAttempts - 1; i++)
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

        for (var i = 0; i < CodeService.MaxAttempts * 2; i++)
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

        for (var i = 0; i < CodeService.MaxAttempts - 1; i++)
            await _service.VerifyCodeAsync(ALICE, "WRONG-23", CodePurpose.ForgottenPassword);

        var replacement = await _service.CreateCodeAsync(alice, CodePurpose.ForgottenPassword);

        for (var i = 0; i < CodeService.MaxAttempts - 1; i++)
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
    public void Tracker_CountsPerCodeAndForgets()
    {
        var tracker = new CodeAttemptTracker();
        var retention = TimeSpan.FromMinutes(30);

        Assert.Multiple(() =>
        {
            Assert.That(tracker.Register(1, retention), Is.EqualTo(1));
            Assert.That(tracker.Register(1, retention), Is.EqualTo(2));
            Assert.That(tracker.Register(2, retention), Is.EqualTo(1));
        });

        tracker.Forget(1);

        Assert.Multiple(() =>
        {
            Assert.That(tracker.Register(1, retention), Is.EqualTo(1));
            Assert.That(tracker.Register(2, retention), Is.EqualTo(2));
        });
    }

    [Test]
    public void Tracker_ParallelGuesses_AreCountedOneByOne()
    {
        var tracker = new CodeAttemptTracker();
        var counts = new System.Collections.Concurrent.ConcurrentBag<int>();

        Parallel.For(0, 500, _ => counts.Add(tracker.Register(7, TimeSpan.FromMinutes(30))));

        Assert.Multiple(() =>
        {
            Assert.That(counts.OrderBy(x => x), Is.EqualTo(Enumerable.Range(1, 500)));
            Assert.That(counts.Count(x => x <= CodeService.MaxAttempts), Is.EqualTo(CodeService.MaxAttempts));
        });
    }

    [Test]
    public void Tracker_DropsCountsOlderThanTheRetention()
    {
        var tracker = new CodeAttemptTracker();

        tracker.Register(1, TimeSpan.FromMilliseconds(10));
        Thread.Sleep(50);

        // Counting another code prunes the stale entry, so the first one starts again.
        tracker.Register(2, TimeSpan.FromMilliseconds(10));
        Assert.That(tracker.Register(1, TimeSpan.FromMilliseconds(10)), Is.EqualTo(1));
    }

    private async Task<int> CreateUserAsync(string email)
    {
        var user = new User
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = email,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return user.Id;
    }

    private async Task<User> GetUserAsync(int id) => await _db.Users.SingleAsync(x => x.Id == id);

    private async Task AddCodeAsync(int userId, CodePurpose purpose, string value, DateTime createdAt)
    {
        _db.Codes.Add(
            new Code
            {
                UserId = userId,
                CodePurpose = purpose,
                Value = value,
                CreatedAt = createdAt
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
