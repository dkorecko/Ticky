using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using BaseConstants = Ticky.Base.Constants;
using Ticky.Internal.Data;

namespace Ticky.Units;

/// <summary>Runs ApiTokenService against an in-memory SQLite database.</summary>
public class ApiTokenServiceTest
{
    private SqliteConnection _connection = null!;
    private TestDbContextFactory _factory = null!;
    private ApiTokenService _service = null!;

    private int _userId;
    private int _otherUserId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _factory = new TestDbContextFactory(
            new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options
        );
        _service = new ApiTokenService(_factory);

        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();

        var user = new User { DisplayName = "User", UserName = "user@test" };
        var otherUser = new User { DisplayName = "Other", UserName = "other@test" };
        db.Users.AddRange(user, otherUser);
        await db.SaveChangesAsync();

        (_userId, _otherUserId) = (user.Id, otherUser.Id);
    }

    [TearDown]
    public void TearDown() => _connection.Dispose();

    [Test]
    public async Task Create_StoresOnlyAHashOfThePlaintext()
    {
        var created = await _service.CreateAsync(_userId, "  Script  ", ApiTokenScope.ReadOnly, null);

        using var db = _factory.CreateDbContext();
        var stored = await db.ApiTokens.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(created.Plaintext, Does.StartWith(BaseConstants.Api.TokenPrefix));
            Assert.That(stored.Name, Is.EqualTo("Script"));
            Assert.That(stored.TokenHash, Has.Length.EqualTo(64));
            Assert.That(stored.TokenHash, Does.Not.Contain(created.Plaintext[BaseConstants.Api.TokenPrefix.Length..]));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Create_BlankName_Throws(string name) =>
        Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(_userId, name, ApiTokenScope.ReadOnly, null));

    [Test]
    public void Create_NameTooLong_Throws() =>
        Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(_userId, new string('a', BaseConstants.Api.TokenNameMaxLength + 1), ApiTokenScope.ReadOnly, null)
        );

    [Test]
    public async Task Validate_ValidToken_ReturnsUserAndScope()
    {
        var created = await _service.CreateAsync(_userId, "Agent", ApiTokenScope.ReadWrite, DateTime.Now.AddDays(1));

        var validated = await _service.ValidateAsync(created.Plaintext);

        Assert.That(validated, Is.Not.Null);
        Assert.That(validated!.User.Id, Is.EqualTo(_userId));
        Assert.That(validated.Scope, Is.EqualTo(ApiTokenScope.ReadWrite));
    }

    [TestCase("")]
    [TestCase("not-a-ticky-token")]
    [TestCase("tky_unknown")]
    public async Task Validate_UnknownOrMalformed_Null(string plaintext)
    {
        await _service.CreateAsync(_userId, "Agent", ApiTokenScope.ReadOnly, null);
        Assert.That(await _service.ValidateAsync(plaintext), Is.Null);
    }

    [Test]
    public async Task Validate_Revoked_Null()
    {
        var created = await _service.CreateAsync(_userId, "Agent", ApiTokenScope.ReadOnly, null);
        Assert.That(await _service.RevokeAsync(_userId, created.Token.Id), Is.True);

        Assert.That(await _service.ValidateAsync(created.Plaintext), Is.Null);
    }

    [Test]
    public async Task Validate_Expired_Null()
    {
        var created = await _service.CreateAsync(_userId, "Agent", ApiTokenScope.ReadOnly, DateTime.Now.AddMinutes(-1));
        Assert.That(await _service.ValidateAsync(created.Plaintext), Is.Null);
    }

    [Test]
    public async Task Validate_AccountNeedsNewCredentials_NullUntilTheyAreSet()
    {
        var created = await _service.CreateAsync(_userId, "Agent", ApiTokenScope.ReadOnly, null);

        await UpdateUserAsync(_userId, x => x.NeedsNewCredentials = true);
        Assert.That(await _service.ValidateAsync(created.Plaintext), Is.Null);

        await UpdateUserAsync(_userId, x => x.NeedsNewCredentials = false);
        Assert.That(await _service.ValidateAsync(created.Plaintext), Is.Not.Null);
    }

    [Test]
    public async Task Validate_AccountLockedOut_NullUntilTheLockoutEnds()
    {
        var created = await _service.CreateAsync(_userId, "Agent", ApiTokenScope.ReadOnly, null);

        await UpdateUserAsync(_userId, x => x.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15));
        Assert.That(await _service.ValidateAsync(created.Plaintext), Is.Null);

        await UpdateUserAsync(_userId, x => x.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.That(await _service.ValidateAsync(created.Plaintext), Is.Not.Null);
    }

    [Test]
    public async Task Validate_OtherAccountsState_DoesNotAffectTheToken()
    {
        var created = await _service.CreateAsync(_userId, "Agent", ApiTokenScope.ReadOnly, null);

        await UpdateUserAsync(_otherUserId, x =>
        {
            x.NeedsNewCredentials = true;
            x.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
        });

        Assert.That(await _service.ValidateAsync(created.Plaintext), Is.Not.Null);
    }

    [Test]
    public async Task Validate_RejectedForAccountState_DoesNotRecordUse()
    {
        var created = await _service.CreateAsync(_userId, "Agent", ApiTokenScope.ReadOnly, null);
        await UpdateUserAsync(_userId, x => x.NeedsNewCredentials = true);

        await _service.ValidateAsync(created.Plaintext);

        Assert.That(await GetLastUsedAsync(created.Token.Id), Is.Null);
    }

    [Test]
    public async Task Validate_WritesLastUsedAtMostOncePerPrecision()
    {
        var created = await _service.CreateAsync(_userId, "Agent", ApiTokenScope.ReadOnly, null);
        var recent = DateTime.Now.AddSeconds(-30);
        var old = DateTime.Now - BaseConstants.Api.LastUsedPrecision - TimeSpan.FromSeconds(30);

        await SetLastUsedAsync(created.Token.Id, recent);
        await _service.ValidateAsync(created.Plaintext);
        Assert.That(await GetLastUsedAsync(created.Token.Id), Is.EqualTo(recent));

        await SetLastUsedAsync(created.Token.Id, old);
        await _service.ValidateAsync(created.Plaintext);
        Assert.That(await GetLastUsedAsync(created.Token.Id), Is.GreaterThan(recent));
    }

    [Test]
    public async Task Revoke_SomeoneElsesToken_FalseAndStillValid()
    {
        var created = await _service.CreateAsync(_userId, "Agent", ApiTokenScope.ReadOnly, null);

        Assert.That(await _service.RevokeAsync(_otherUserId, created.Token.Id), Is.False);
        Assert.That(await _service.ValidateAsync(created.Plaintext), Is.Not.Null);
    }

    [Test]
    public async Task List_OnlyTheUsersTokens()
    {
        await _service.CreateAsync(_userId, "Mine", ApiTokenScope.ReadOnly, null);
        await _service.CreateAsync(_otherUserId, "Theirs", ApiTokenScope.ReadOnly, null);

        var tokens = await _service.ListAsync(_userId);
        Assert.That(tokens.Select(x => x.Name), Is.EqualTo(new[] { "Mine" }));
    }

    private async Task UpdateUserAsync(int userId, Action<User> change)
    {
        using var db = _factory.CreateDbContext();
        change(await db.Users.SingleAsync(x => x.Id == userId));
        await db.SaveChangesAsync();
    }

    private async Task SetLastUsedAsync(int tokenId, DateTime value)
    {
        using var db = _factory.CreateDbContext();
        (await db.ApiTokens.SingleAsync(x => x.Id == tokenId)).LastUsedAt = value;
        await db.SaveChangesAsync();
    }

    private async Task<DateTime?> GetLastUsedAsync(int tokenId)
    {
        using var db = _factory.CreateDbContext();
        return (await db.ApiTokens.SingleAsync(x => x.Id == tokenId)).LastUsedAt;
    }

    private class TestDbContextFactory(DbContextOptions<DataContext> options) : IDbContextFactory<DataContext>
    {
        public DataContext CreateDbContext() => new(options);
    }
}
