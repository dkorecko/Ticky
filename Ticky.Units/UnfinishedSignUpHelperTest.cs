using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticky.Internal.Data;

namespace Ticky.Units;

/// <summary>Runs the unfinished sign-up query against an in-memory SQLite database.</summary>
public class UnfinishedSignUpHelperTest
{
    private SqliteConnection _connection = null!;
    private DataContext _db = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new DataContext(new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task UnconfirmedAccountWithPendingNewAccountCode_IsUnfinished()
    {
        var user = await AddUserAsync("pending", confirmed: false, CodePurpose.NewAccount);

        Assert.That(await UnfinishedIdsAsync(), Is.EqualTo(new[] { user.Id }));
    }

    [Test]
    public async Task OnlyTheAccountsWaitingForConfirmationAreReturned()
    {
        var pending = await AddUserAsync("pending", confirmed: false, CodePurpose.NewAccount);
        await AddUserAsync("confirmed", confirmed: true, code: null);
        await AddUserAsync("legacy", confirmed: false, code: null);
        await AddUserAsync("resetting", confirmed: false, CodePurpose.ForgottenPassword);
        await AddUserAsync("leftover", confirmed: true, CodePurpose.NewAccount);

        Assert.That(await UnfinishedIdsAsync(), Is.EqualTo(new[] { pending.Id }));
    }

    [Test]
    public async Task AccountIsNoLongerUnfinished_OnceTheAddressIsConfirmed()
    {
        var user = await AddUserAsync("pending", confirmed: false, CodePurpose.NewAccount);

        user.EmailConfirmed = true;
        await _db.SaveChangesAsync();

        Assert.That(await UnfinishedIdsAsync(), Is.Empty);
    }

    [Test]
    public async Task AccountIsNoLongerUnfinished_OnceTheCodeIsGone()
    {
        var user = await AddUserAsync("pending", confirmed: false, CodePurpose.NewAccount);

        _db.Codes.Remove(await _db.Codes.SingleAsync(x => x.UserId == user.Id));
        await _db.SaveChangesAsync();

        Assert.That(await UnfinishedIdsAsync(), Is.Empty);
    }

    private async Task<int[]> UnfinishedIdsAsync() =>
        await _db.Users.WithUnfinishedSignUp().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();

    private async Task<User> AddUserAsync(string name, bool confirmed, CodePurpose? code)
    {
        var user = new User
        {
            DisplayName = name,
            UserName = $"{name}@test.com",
            Email = $"{name}@test.com",
            EmailConfirmed = confirmed
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        if (code is not null)
        {
            _db.Codes.Add(new Code { Value = "123-456", CodePurpose = code.Value, UserId = user.Id });
            await _db.SaveChangesAsync();
        }

        return user;
    }
}
