using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Ticky.Base.DTOs;
using Ticky.Internal.Data;
using AppConstants = Ticky.Base.Constants;

namespace Ticky.Units;

/// <summary>Runs UserAccountService with real Identity services against an in-memory SQLite database.</summary>
public class UserAccountServiceTest
{
    private const string PASSWORD = "secret1";
    private const string NEW_PASSWORD = "brand-new-1";

    private static readonly byte[] PNG_BYTES = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
    );

    private SqliteConnection _connection = null!;
    private ServiceProvider _provider = null!;
    private UserAccountService _service = null!;
    private bool _wasOffline;
    private HashSet<string> _existingImages = [];

    private int _adminId;
    private int _userId;

    [SetUp]
    public async Task Setup()
    {
        _wasOffline = AppConstants.FULLY_OFFLINE;
        AppConstants.FULLY_OFFLINE = false;
        Directory.CreateDirectory(AppConstants.SAVE_UPLOADED_IMAGES_PATH);
        _existingImages = [.. Directory.GetFiles(AppConstants.SAVE_UPLOADED_IMAGES_PATH)];

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DataContext>(options => options.UseSqlite(_connection));
        // The same rules as Program.cs.
        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<DataContext>();
        services.AddScoped(_ => new AvatarService(new HttpClient(new StubAvatarHandler())));
        _provider = services.BuildServiceProvider();

        _service = new UserAccountService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<UserAccountService>.Instance
        );

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<DataContext>().Database.EnsureCreated();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        _adminId = await CreateUserAsync(userManager, "Admin", "admin@test.com");
        await userManager.AddClaimAsync(
            (await userManager.FindByIdAsync(_adminId.ToString()))!,
            new Claim(ClaimTypes.Role, AppConstants.Roles.Admin)
        );
        _userId = await CreateUserAsync(userManager, "Jane Doe", "jane@test.com");
    }

    [TearDown]
    public void TearDown()
    {
        AppConstants.FULLY_OFFLINE = _wasOffline;
        _provider.Dispose();
        _connection.Dispose();

        foreach (var file in Directory.GetFiles(AppConstants.SAVE_UPLOADED_IMAGES_PATH).Except(_existingImages))
            File.Delete(file);
    }

    // Admin: editing users

    [Test]
    public async Task UpdateUser_NameOnly_KeepsPasswordAndSignIns()
    {
        var before = await GetUserAsync(_userId);

        var result = await _service.UpdateUserAsync(_adminId, _userId, "Jane Smith", "jane@test.com", null, true);
        var after = await GetUserAsync(_userId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.DisplayNameChanged, Is.True);
            Assert.That(result.PasswordChanged, Is.False);
            Assert.That(after.DisplayName, Is.EqualTo("Jane Smith"));
            Assert.That(after.NeedsNewCredentials, Is.False);
            Assert.That(after.SecurityStamp, Is.EqualTo(before.SecurityStamp));
            Assert.That(await PasswordWorksAsync(_userId, PASSWORD), Is.True);
        });
    }

    [Test]
    public async Task UpdateUser_NewEmail_ChangesUserNameAlongWithIt()
    {
        var before = await GetUserAsync(_userId);

        var result = await _service.UpdateUserAsync(_adminId, _userId, "Jane Doe", "  Jane.New@Test.com ", null, true);
        var after = await GetUserAsync(_userId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.EmailChanged, Is.True);
            Assert.That(after.Email, Is.EqualTo("Jane.New@Test.com"));
            Assert.That(after.UserName, Is.EqualTo("Jane.New@Test.com"));
            Assert.That(after.NormalizedEmail, Is.EqualTo("JANE.NEW@TEST.COM"));
            Assert.That(after.NormalizedUserName, Is.EqualTo("JANE.NEW@TEST.COM"));
            Assert.That(after.EmailConfirmed, Is.True);
            Assert.That(after.SecurityStamp, Is.EqualTo(before.SecurityStamp));
            Assert.That((await FindByNameAsync("jane.new@test.com"))?.Id, Is.EqualTo(_userId));
        });
    }

    [TestCase("admin@test.com")]
    [TestCase(" ADMIN@Test.com ")]
    public async Task UpdateUser_EmailOfAnotherUser_IsRefusedAndNothingIsSaved(string email)
    {
        var result = await _service.UpdateUserAsync(_adminId, _userId, "Jane Smith", email, null, true);
        var after = await GetUserAsync(_userId);

        Assert.Multiple(() =>
        {
            Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.Email }));
            Assert.That(after.Email, Is.EqualTo("jane@test.com"));
            Assert.That(after.DisplayName, Is.EqualTo("Jane Doe"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task UpdateUser_NewPassword_ReplacesItAndAsksForAChangeOnlyWhenRequested(bool requireChange)
    {
        var before = await GetUserAsync(_userId);

        var result = await _service.UpdateUserAsync(_adminId, _userId, "Jane Doe", "jane@test.com", NEW_PASSWORD, requireChange);
        var after = await GetUserAsync(_userId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.PasswordChanged, Is.True);
            Assert.That(after.NeedsNewCredentials, Is.EqualTo(requireChange));
            Assert.That(after.SecurityStamp, Is.Not.EqualTo(before.SecurityStamp));
            Assert.That(await PasswordWorksAsync(_userId, NEW_PASSWORD), Is.True);
            Assert.That(await PasswordWorksAsync(_userId, PASSWORD), Is.False);
        });
    }

    [Test]
    public async Task UpdateUser_TooShortPassword_IsRefusedOnThatFieldAndNothingIsSaved()
    {
        var result = await _service.UpdateUserAsync(_adminId, _userId, "Jane Smith", "jane@test.com", "abc", true);
        var after = await GetUserAsync(_userId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.NewPassword }));
            Assert.That(after.DisplayName, Is.EqualTo("Jane Doe"));
            Assert.That(after.NeedsNewCredentials, Is.False);
            Assert.That(await PasswordWorksAsync(_userId, PASSWORD), Is.True);
        });
    }

    [Test]
    public async Task UpdateUser_Self_CanChangeNameAndEmailButNotPassword()
    {
        var before = await GetUserAsync(_adminId);

        var passwordResult = await _service.UpdateUserAsync(_adminId, _adminId, "Admin", "admin@test.com", NEW_PASSWORD, true);
        var result = await _service.UpdateUserAsync(_adminId, _adminId, "Boss", "boss@test.com", null, true);
        var after = await GetUserAsync(_adminId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(FieldsOf(passwordResult), Is.EqualTo(new[] { UserAccountField.NewPassword }));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(after.UserName, Is.EqualTo("boss@test.com"));
            Assert.That(after.NeedsNewCredentials, Is.False);
            Assert.That(after.SecurityStamp, Is.EqualTo(before.SecurityStamp));
            Assert.That(await PasswordWorksAsync(_adminId, PASSWORD), Is.True);
        });
    }

    [Test]
    public async Task UpdateUser_ByNonAdmin_IsRefused()
    {
        var result = await _service.UpdateUserAsync(_userId, _adminId, "Hacked", "admin@test.com", null, true);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.General }));
            Assert.That((await GetUserAsync(_adminId)).DisplayName, Is.EqualTo("Admin"));
        });
    }

    [Test]
    public async Task UpdateUser_NothingChanged_SavesNothing()
    {
        var before = await GetUserAsync(_userId);

        var result = await _service.UpdateUserAsync(_adminId, _userId, " Jane Doe ", "jane@test.com", "", true);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.AnythingChanged, Is.False);
            Assert.That((await GetUserAsync(_userId)).ConcurrencyStamp, Is.EqualTo(before.ConcurrencyStamp));
        });
    }

    [TestCase("   ")]
    [TestCase("Name\nwith a line break")]
    public async Task UpdateUser_UnusableName_IsRefusedOnThatField(string name)
    {
        var result = await _service.UpdateUserAsync(_adminId, _userId, name, "jane@test.com", null, true);

        Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.DisplayName }));
    }

    [Test]
    public async Task UpdateUser_TooLongName_IsRefusedButLongestAllowedNameIsSaved()
    {
        var longest = new string('a', AppConstants.Limits.MAX_DISPLAY_NAME_LENGTH);

        var tooLong = await _service.UpdateUserAsync(_adminId, _userId, longest + "a", "jane@test.com", null, true);
        var allowed = await _service.UpdateUserAsync(_adminId, _userId, longest, "jane@test.com", null, true);

        Assert.Multiple(() =>
        {
            Assert.That(FieldsOf(tooLong), Is.EqualTo(new[] { UserAccountField.DisplayName }));
            Assert.That(allowed.Succeeded, Is.True);
        });
    }

    [Test]
    public async Task UpdateUser_UserWhoseUserNameDrifted_SavingTheSameEmailRepairsIt()
    {
        await SetUserNameDirectlyAsync(_userId, "old@test.com");

        var result = await _service.UpdateUserAsync(_adminId, _userId, "Jane Doe", "jane@test.com", null, true);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That((await GetUserAsync(_userId)).UserName, Is.EqualTo("jane@test.com"));
        });
    }

    // Admin: creating and deleting users

    [Test]
    public async Task CreateUser_ConfirmsEmailAndAsksForNewCredentials()
    {
        var result = await _service.CreateUserAsync(_adminId, " New Person ", " New@Test.com ", PASSWORD);
        var created = await FindByNameAsync("new@test.com");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.DisplayName, Is.EqualTo("New Person"));
            Assert.That(created.UserName, Is.EqualTo("New@Test.com"));
            Assert.That(created.Email, Is.EqualTo("New@Test.com"));
            Assert.That(created.EmailConfirmed, Is.True);
            Assert.That(created.NeedsNewCredentials, Is.True);
            Assert.That(AvatarService.IsGenerated(created.ProfilePictureFileName), Is.True);
        });
    }

    [Test]
    public async Task CreateUser_TakenEmail_IsRefusedOnThatField()
    {
        var result = await _service.CreateUserAsync(_adminId, "Someone", "JANE@test.com", PASSWORD);

        Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.Email }));
    }

    [Test]
    public async Task CreateUser_TooShortPassword_IsRefusedOnThatField()
    {
        var result = await _service.CreateUserAsync(_adminId, "Someone", "someone@test.com", "abc");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.NewPassword }));
            Assert.That(await FindByNameAsync("someone@test.com"), Is.Null);
        });
    }

    [Test]
    public async Task CreateUser_ByNonAdmin_IsRefused()
    {
        var result = await _service.CreateUserAsync(_userId, "Someone", "someone@test.com", PASSWORD);

        Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.General }));
    }

    [Test]
    public async Task DeleteUser_Self_IsRefusedSoTheLastAdminRemains()
    {
        var result = await _service.DeleteUserAsync(_adminId, _adminId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(await _service.GetAdminIdsAsync(), Is.EqualTo(new[] { _adminId }));
        });
    }

    [Test]
    public async Task DeleteUser_OtherUser_DeletesThem()
    {
        var result = await _service.DeleteUserAsync(_adminId, _userId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(await FindByNameAsync("jane@test.com"), Is.Null);
        });
    }

    [Test]
    public async Task DeleteUser_ByNonAdmin_IsRefused()
    {
        var result = await _service.DeleteUserAsync(_userId, _adminId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(await FindByNameAsync("admin@test.com"), Is.Not.Null);
        });
    }

    // Self-service: name

    [Test]
    public async Task ChangeDisplayName_TrimsTheName_AndTheSameNameChangesNothing()
    {
        var changed = await _service.ChangeDisplayNameAsync(_userId, "  Jane Smith ");
        var unchanged = await _service.ChangeDisplayNameAsync(_userId, "Jane Smith");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(changed.DisplayNameChanged, Is.True);
            Assert.That(unchanged.Succeeded, Is.True);
            Assert.That(unchanged.DisplayNameChanged, Is.False);
            Assert.That((await GetUserAsync(_userId)).DisplayName, Is.EqualTo("Jane Smith"));
        });
    }

    [Test]
    public async Task ChangeDisplayName_GeneratedAvatar_IsRegenerated()
    {
        await SetAvatarAsync(_userId, AvatarService.GENERATED_FILE_PREFIX + "oldavatr.png");

        await _service.ChangeDisplayNameAsync(_userId, "Jane Smith");
        var avatar = (await GetUserAsync(_userId)).ProfilePictureFileName;

        Assert.Multiple(() =>
        {
            Assert.That(AvatarService.IsGenerated(avatar), Is.True);
            Assert.That(avatar, Is.Not.EqualTo(AvatarService.GENERATED_FILE_PREFIX + "oldavatr.png"));
        });
    }

    [Test]
    public async Task ChangeDisplayName_UploadedAvatar_IsKept()
    {
        await SetAvatarAsync(_userId, "uploaded.png");

        await _service.ChangeDisplayNameAsync(_userId, "Jane Smith");

        Assert.That((await GetUserAsync(_userId)).ProfilePictureFileName, Is.EqualTo("uploaded.png"));
    }

    [Test]
    public async Task ChangeDisplayName_Offline_KeepsGeneratedAvatarBecauseItDoesNotShowTheName()
    {
        AppConstants.FULLY_OFFLINE = true;
        var avatar = AvatarService.GENERATED_FILE_PREFIX + "oldavatr.png";
        await SetAvatarAsync(_userId, avatar);

        await _service.ChangeDisplayNameAsync(_userId, "Jane Smith");

        Assert.That((await GetUserAsync(_userId)).ProfilePictureFileName, Is.EqualTo(avatar));
    }

    // Self-service: e-mail

    [Test]
    public async Task ChangeEmail_WrongCurrentPassword_IsRefusedOnThatField()
    {
        var result = await _service.ChangeEmailAsync(_userId, "wrong-password", "jane.new@test.com");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.CurrentPassword }));
            Assert.That((await GetUserAsync(_userId)).Email, Is.EqualTo("jane@test.com"));
        });
    }

    [Test]
    public async Task ChangeEmail_SameAddress_IsRefusedOnTheEmailField()
    {
        var result = await _service.ChangeEmailAsync(_userId, PASSWORD, " jane@test.com ");

        Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.Email }));
    }

    [Test]
    public async Task ChangeEmail_OnlyCaseDiffers_IsSaved()
    {
        var result = await _service.ChangeEmailAsync(_userId, PASSWORD, "Jane@Test.com");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.EmailChanged, Is.True);
            Assert.That((await GetUserAsync(_userId)).UserName, Is.EqualTo("Jane@Test.com"));
        });
    }

    [Test]
    public async Task ChangeEmail_TakenAddress_IsRefusedOnTheEmailField()
    {
        var result = await _service.ChangeEmailAsync(_userId, PASSWORD, "admin@test.com");

        Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.Email }));
    }

    [Test]
    public async Task ChangeEmail_Valid_ChangesTheSignInNameAndKeepsSignIns()
    {
        var before = await GetUserAsync(_userId);

        var result = await _service.ChangeEmailAsync(_userId, PASSWORD, "jane.new@test.com");
        var after = await GetUserAsync(_userId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.PasswordChanged, Is.False);
            Assert.That(after.UserName, Is.EqualTo("jane.new@test.com"));
            Assert.That(after.SecurityStamp, Is.EqualTo(before.SecurityStamp));
            Assert.That(await PasswordWorksAsync(_userId, PASSWORD), Is.True);
        });
    }

    // Self-service: password

    [Test]
    public async Task ChangePassword_WrongCurrentPassword_IsRefusedOnThatField()
    {
        var result = await _service.ChangePasswordAsync(_userId, "wrong-password", NEW_PASSWORD);

        Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.CurrentPassword }));
    }

    [Test]
    public async Task ChangePassword_SameAsCurrent_IsRefusedOnTheNewPasswordField()
    {
        var result = await _service.ChangePasswordAsync(_userId, PASSWORD, PASSWORD);

        Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.NewPassword }));
    }

    [Test]
    public async Task ChangePassword_Valid_ReplacesItAndEndsExistingSignIns()
    {
        var before = await GetUserAsync(_userId);

        var result = await _service.ChangePasswordAsync(_userId, PASSWORD, NEW_PASSWORD);
        var after = await GetUserAsync(_userId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.PasswordChanged, Is.True);
            Assert.That(after.SecurityStamp, Is.Not.EqualTo(before.SecurityStamp));
            Assert.That(after.Email, Is.EqualTo("jane@test.com"));
            Assert.That(await PasswordWorksAsync(_userId, NEW_PASSWORD), Is.True);
        });
    }

    // Self-service: credentials that must be replaced

    [Test]
    public async Task SetRequiredCredentials_WhenNotRequired_IsRefused()
    {
        var result = await _service.SetRequiredCredentialsAsync(_userId, await SessionOfAsync(_userId), "jane@test.com", NEW_PASSWORD);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.General }));
            Assert.That(await PasswordWorksAsync(_userId, PASSWORD), Is.True);
        });
    }

    [Test]
    public async Task SetRequiredCredentials_SessionFromBeforeAnAdminReset_MustSignInAgain()
    {
        var oldSession = await SessionOfAsync(_userId);
        await _service.UpdateUserAsync(_adminId, _userId, "Jane Doe", "jane@test.com", NEW_PASSWORD, true);

        var result = await _service.SetRequiredCredentialsAsync(_userId, oldSession, "jane@test.com", "mine-now-1");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.SignInRequired, Is.True);
            Assert.That(await PasswordWorksAsync(_userId, NEW_PASSWORD), Is.True);
        });
    }

    [Test]
    public async Task SetRequiredCredentials_Valid_SetsEmailAndPasswordWithoutTheCurrentOne()
    {
        await _service.UpdateUserAsync(_adminId, _userId, "Jane Doe", "jane@test.com", NEW_PASSWORD, true);

        var result = await _service.SetRequiredCredentialsAsync(_userId, await SessionOfAsync(_userId), "jane.own@test.com", "mine-now-1");
        var after = await GetUserAsync(_userId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(after.NeedsNewCredentials, Is.False);
            Assert.That(after.UserName, Is.EqualTo("jane.own@test.com"));
            Assert.That(await PasswordWorksAsync(_userId, "mine-now-1"), Is.True);
        });
    }

    [Test]
    public async Task SetRequiredCredentials_KeepingTheGivenPassword_IsRefused()
    {
        await _service.UpdateUserAsync(_adminId, _userId, "Jane Doe", "jane@test.com", NEW_PASSWORD, true);

        var result = await _service.SetRequiredCredentialsAsync(_userId, await SessionOfAsync(_userId), "jane@test.com", NEW_PASSWORD);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(FieldsOf(result), Is.EqualTo(new[] { UserAccountField.NewPassword }));
            Assert.That((await GetUserAsync(_userId)).NeedsNewCredentials, Is.True);
        });
    }

    // Start-up repair of user names

    [Test]
    public async Task UserNameEmailSync_GivesDriftedAccountsTheirEmailAsUserName_UnlessAnotherAccountHasIt()
    {
        await SetUserNameDirectlyAsync(_userId, "old@test.com");
        using (var scope = _provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var conflicting = await CreateUserAsync(userManager, "Conflict", "conflict@test.com");
            // Drifted to an e-mail that is the admin's user name, as the old admin dialog allowed.
            await scope
                .ServiceProvider.GetRequiredService<DataContext>()
                .Users.Where(x => x.Id == conflicting)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Email, "admin@test.com").SetProperty(y => y.NormalizedEmail, "ADMIN@TEST.COM"));
        }

        using (var scope = _provider.CreateScope())
            await UserNameEmailSync.RunAsync(
                scope.ServiceProvider.GetRequiredService<DataContext>(),
                scope.ServiceProvider.GetRequiredService<UserManager<User>>(),
                NullLogger.Instance
            );

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await FindByNameAsync("jane@test.com"))?.Id, Is.EqualTo(_userId));
            Assert.That((await FindByNameAsync("conflict@test.com"))?.Email, Is.EqualTo("admin@test.com"));
            Assert.That((await FindByNameAsync("admin@test.com"))?.Id, Is.EqualTo(_adminId));
        });
    }

    private static UserAccountField[] FieldsOf(UserAccountResult result) =>
        result.Errors.Select(x => x.Field).Distinct().ToArray();

    private static async Task<int> CreateUserAsync(UserManager<User> userManager, string name, string email)
    {
        var user = new User { DisplayName = name, UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, PASSWORD);
        Assert.That(result.Succeeded, Is.True, string.Join(", ", result.Errors.Select(x => x.Description)));
        return user.Id;
    }

    private async Task<User> GetUserAsync(int id)
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DataContext>().Users.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task<User?> FindByNameAsync(string userName)
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByNameAsync(userName);
    }

    private async Task<bool> PasswordWorksAsync(int id, string password)
    {
        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        return await userManager.CheckPasswordAsync((await userManager.FindByIdAsync(id.ToString()))!, password);
    }

    /// <summary>The claims a sign-in made right now would carry.</summary>
    private async Task<ClaimsPrincipal> SessionOfAsync(int id)
    {
        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<User>>();
        return await factory.CreateAsync((await userManager.FindByIdAsync(id.ToString()))!);
    }

    private async Task SetAvatarAsync(int id, string fileName)
    {
        using var scope = _provider.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<DataContext>()
            .Users.Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ProfilePictureFileName, fileName));
    }

    /// <summary>Changes only the user name, like the old admin dialog changed only the e-mail.</summary>
    private async Task SetUserNameDirectlyAsync(int id, string userName)
    {
        using var scope = _provider.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<DataContext>()
            .Users.Where(x => x.Id == id)
            .ExecuteUpdateAsync(x =>
                x.SetProperty(y => y.UserName, userName).SetProperty(y => y.NormalizedUserName, userName.ToUpperInvariant())
            );
    }

    private class StubAvatarHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(PNG_BYTES) });
    }
}
