using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace Ticky.Internal.Services;

/// <summary>
/// Creates, changes and deletes accounts, both for administrators and for people changing their own account, so
/// every path follows the same rules. The e-mail address is also the user name people sign in with, so the two always
/// change together.
/// </summary>
/// <remarks>
/// Each call works in a service scope of its own. Blazor keeps one scope for the whole circuit, so a
/// <see cref="UserManager{TUser}"/> injected into a component keeps the users it has loaded and would later save
/// those stale copies over newer changes. Each call also saves the account at most once, so a change either applies
/// fully or not at all.
/// </remarks>
public class UserAccountService(IServiceScopeFactory scopeFactory, ILogger<UserAccountService> logger)
{
    private const string EMAIL_TAKEN = "This email address is already used by another account.";
    private const string WRONG_CURRENT_PASSWORD =
        "Your current password is incorrect. If you've forgotten it, ask an administrator to reset it.";
    private const string NOT_ADMIN = "Only administrators can manage users.";
    private const string USER_NOT_FOUND = "This user no longer exists.";

    private static readonly EmailAddressAttribute EmailValidator = new();

    public async Task<HashSet<int>> GetAdminIdsAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var ids = await db
            .UserClaims.Where(x => x.ClaimType == ClaimTypes.Role && x.ClaimValue == Constants.Roles.Admin)
            .Select(x => x.UserId)
            .ToListAsync();

        return [.. ids];
    }

    /// <summary>
    /// Creates an account for someone else. They have to replace the password when they first sign in.
    /// </summary>
    public async Task<UserAccountResult> CreateUserAsync(int actingUserId, string displayName, string email, string password)
    {
        var result = new UserAccountResult();
        var name = ValidateDisplayName(displayName, result);
        var address = ValidateEmail(email, result);

        if (string.IsNullOrEmpty(password))
            result.AddError(UserAccountField.NewPassword, "A password is required.");

        if (!result.Succeeded)
            return result;

        await using var scope = scopeFactory.CreateAsyncScope();
        var (db, userManager) = Resolve(scope);

        if (!await IsAdminAsync(db, actingUserId))
            return UserAccountResult.Fail(UserAccountField.General, NOT_ADMIN);

        if (await IsEmailTakenAsync(db, userManager, address!))
            return UserAccountResult.Fail(UserAccountField.Email, EMAIL_TAKEN);

        var user = new User
        {
            DisplayName = name!,
            UserName = address,
            Email = address,
            // The administrator vouches for the address. Unconfirmed accounts are treated as unfinished sign-ups.
            EmailConfirmed = true,
            NeedsNewCredentials = true,
            ProfilePictureFileName = await TryGenerateAvatarAsync(scope.ServiceProvider, name!)
        };

        await SaveAsync(user, result, () => userManager.CreateAsync(user, password));
        return result;
    }

    /// <summary>
    /// An administrator's changes to an account. The name and e-mail are always given; <paramref name="newPassword"/>
    /// is optional, and only when it is set does <paramref name="requirePasswordChange"/> decide whether the user
    /// has to replace it at their next sign-in. Administrators change their own password in their settings instead,
    /// where the current password is checked.
    /// </summary>
    public async Task<UserAccountResult> UpdateUserAsync(
        int actingUserId,
        int userId,
        string displayName,
        string email,
        string? newPassword,
        bool requirePasswordChange
    )
    {
        var result = new UserAccountResult();
        var name = ValidateDisplayName(displayName, result);
        var address = ValidateEmail(email, result);
        var setsPassword = !string.IsNullOrEmpty(newPassword);

        if (setsPassword && actingUserId == userId)
            result.AddError(
                UserAccountField.NewPassword,
                "Change your own password in your settings, where your current password is checked."
            );

        if (!result.Succeeded)
            return result;

        await using var scope = scopeFactory.CreateAsyncScope();
        var (db, userManager) = Resolve(scope);

        if (!await IsAdminAsync(db, actingUserId))
            return UserAccountResult.Fail(UserAccountField.General, NOT_ADMIN);

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return UserAccountResult.Fail(UserAccountField.General, USER_NOT_FOUND);

        ApplyDisplayName(user, name!, result);
        await ApplyEmailAsync(db, userManager, user, address!, result, unchangedIsError: false);

        if (setsPassword)
        {
            await ApplyPasswordAsync(userManager, user, newPassword!, result);
            user.NeedsNewCredentials = requirePasswordChange;
        }

        if (!result.Succeeded || !result.AnythingChanged)
            return result;

        if (result.DisplayNameChanged)
            await RefreshGeneratedAvatarAsync(scope.ServiceProvider, user);

        await SaveAsync(user, result, () => userManager.UpdateAsync(user));
        return result;
    }

    /// <remarks>
    /// Administrators can't delete themselves, so the administrator doing the deleting always remains and the last
    /// administrator can never be removed.
    /// </remarks>
    public async Task<UserAccountResult> DeleteUserAsync(int actingUserId, int userId)
    {
        if (actingUserId == userId)
            return UserAccountResult.Fail(UserAccountField.General, "You can't delete your own account.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        if (!await IsAdminAsync(db, actingUserId))
            return UserAccountResult.Fail(UserAccountField.General, NOT_ADMIN);

        if (await db.Users.Where(x => x.Id == userId).ExecuteDeleteAsync() == 0)
            return UserAccountResult.Fail(UserAccountField.General, USER_NOT_FOUND);

        return new UserAccountResult();
    }

    public async Task<UserAccountResult> ChangeDisplayNameAsync(int userId, string displayName)
    {
        var result = new UserAccountResult();
        var name = ValidateDisplayName(displayName, result);

        if (!result.Succeeded)
            return result;

        await using var scope = scopeFactory.CreateAsyncScope();
        var (_, userManager) = Resolve(scope);
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return UserAccountResult.Fail(UserAccountField.General, USER_NOT_FOUND);

        ApplyDisplayName(user, name!, result);

        if (!result.DisplayNameChanged)
            return result;

        await RefreshGeneratedAvatarAsync(scope.ServiceProvider, user);
        await SaveAsync(user, result, () => userManager.UpdateAsync(user));
        return result;
    }

    /// <summary>
    /// Changes the address the user signs in with, after checking their current password. Existing sign-ins keep
    /// working: the password, which is what proves who someone is, stays the same.
    /// </summary>
    public async Task<UserAccountResult> ChangeEmailAsync(int userId, string currentPassword, string newEmail)
    {
        var result = new UserAccountResult();
        var address = ValidateEmail(newEmail, result);

        if (string.IsNullOrEmpty(currentPassword))
            result.AddError(UserAccountField.CurrentPassword, "Enter your current password.");

        if (!result.Succeeded)
            return result;

        await using var scope = scopeFactory.CreateAsyncScope();
        var (db, userManager) = Resolve(scope);
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return UserAccountResult.Fail(UserAccountField.General, USER_NOT_FOUND);

        if (!IsCurrentPassword(userManager, user, currentPassword))
            return UserAccountResult.Fail(UserAccountField.CurrentPassword, WRONG_CURRENT_PASSWORD);

        await ApplyEmailAsync(db, userManager, user, address!, result, unchangedIsError: true);

        if (!result.Succeeded)
            return result;

        await SaveAsync(user, result, () => userManager.UpdateAsync(user));
        return result;
    }

    /// <summary>
    /// Changes the user's password after checking the current one. This renews the security stamp, so the user's
    /// existing sign-ins, including the current one, stop being valid.
    /// </summary>
    public async Task<UserAccountResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var result = new UserAccountResult();

        if (string.IsNullOrEmpty(currentPassword))
            result.AddError(UserAccountField.CurrentPassword, "Enter your current password.");

        if (string.IsNullOrEmpty(newPassword))
            result.AddError(UserAccountField.NewPassword, "Enter a new password.");

        if (!result.Succeeded)
            return result;

        await using var scope = scopeFactory.CreateAsyncScope();
        var (_, userManager) = Resolve(scope);
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return UserAccountResult.Fail(UserAccountField.General, USER_NOT_FOUND);

        if (!IsCurrentPassword(userManager, user, currentPassword))
            return UserAccountResult.Fail(UserAccountField.CurrentPassword, WRONG_CURRENT_PASSWORD);

        if (IsCurrentPassword(userManager, user, newPassword))
            return UserAccountResult.Fail(
                UserAccountField.NewPassword,
                "Choose a password that is different from your current one."
            );

        await ApplyPasswordAsync(userManager, user, newPassword, result);

        if (!result.Succeeded)
            return result;

        user.NeedsNewCredentials = false;
        await SaveAsync(user, result, () => userManager.UpdateAsync(user));
        return result;
    }

    /// <summary>
    /// Sets the credentials of an account that has to replace the ones it was given (the default administrator, or
    /// an account an administrator created or reset). The current password isn't asked for, so this only works from
    /// a sign-in made with the current password: a session from before an administrator reset the password must
    /// sign in again first.
    /// </summary>
    public async Task<UserAccountResult> SetRequiredCredentialsAsync(
        int userId,
        ClaimsPrincipal session,
        string email,
        string newPassword
    )
    {
        var result = new UserAccountResult();
        var address = ValidateEmail(email, result);

        if (string.IsNullOrEmpty(newPassword))
            result.AddError(UserAccountField.NewPassword, "Enter a new password.");

        if (!result.Succeeded)
            return result;

        await using var scope = scopeFactory.CreateAsyncScope();
        var (db, userManager) = Resolve(scope);
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return UserAccountResult.Fail(UserAccountField.General, USER_NOT_FOUND);

        if (!user.NeedsNewCredentials)
            return UserAccountResult.Fail(UserAccountField.General, "Your credentials have already been set.");

        var claims = userManager.Options.ClaimsIdentity;

        if (
            session.FindFirstValue(claims.UserIdClaimType) != user.Id.ToString()
            || session.FindFirstValue(claims.SecurityStampClaimType) != user.SecurityStamp
        )
        {
            result.AddError(
                UserAccountField.General,
                "Your password was changed after you signed in. Sign in again with the new one."
            );
            result.SignInRequired = true;
            return result;
        }

        await ApplyEmailAsync(db, userManager, user, address!, result, unchangedIsError: false);

        if (IsCurrentPassword(userManager, user, newPassword))
            result.AddError(
                UserAccountField.NewPassword,
                "Choose a password that is different from the one you signed in with."
            );
        else
            await ApplyPasswordAsync(userManager, user, newPassword, result);

        if (!result.Succeeded)
            return result;

        user.NeedsNewCredentials = false;
        await SaveAsync(user, result, () => userManager.UpdateAsync(user));
        return result;
    }

    private static (DataContext Db, UserManager<User> UserManager) Resolve(AsyncServiceScope scope) =>
        (
            scope.ServiceProvider.GetRequiredService<DataContext>(),
            scope.ServiceProvider.GetRequiredService<UserManager<User>>()
        );

    private static Task<bool> IsAdminAsync(DataContext db, int userId) =>
        db.UserClaims.AnyAsync(x =>
            x.UserId == userId && x.ClaimType == ClaimTypes.Role && x.ClaimValue == Constants.Roles.Admin
        );

    /// <summary>Trims the name and checks it. Returns null, and adds an error, when it can't be used.</summary>
    private static string? ValidateDisplayName(string? displayName, UserAccountResult result)
    {
        var name = displayName?.Trim() ?? string.Empty;

        if (name.Length == 0)
            result.AddError(UserAccountField.DisplayName, "A name is required.");
        else if (name.Length > Constants.Limits.MAX_DISPLAY_NAME_LENGTH)
            result.AddError(
                UserAccountField.DisplayName,
                $"The name can be at most {Constants.Limits.MAX_DISPLAY_NAME_LENGTH} characters long."
            );
        else if (name.Any(char.IsControl))
            result.AddError(UserAccountField.DisplayName, "The name can't contain line breaks or similar characters.");
        else
            return name;

        return null;
    }

    /// <summary>Trims the address and checks it. Returns null, and adds an error, when it can't be used.</summary>
    private static string? ValidateEmail(string? email, UserAccountResult result)
    {
        var address = email?.Trim() ?? string.Empty;

        if (address.Length == 0)
            result.AddError(UserAccountField.Email, "An email address is required.");
        else if (address.Length > Constants.Limits.MAX_EMAIL_LENGTH)
            result.AddError(
                UserAccountField.Email,
                $"The email address can be at most {Constants.Limits.MAX_EMAIL_LENGTH} characters long."
            );
        else if (!EmailValidator.IsValid(address))
            result.AddError(UserAccountField.Email, "This is not a valid email address.");
        else
            return address;

        return null;
    }

    /// <summary>
    /// Whether another account already uses the address, as its e-mail or as its user name. Identity doesn't
    /// require unique e-mails here, and accounts from before user names followed e-mails can have them differ.
    /// </summary>
    private static Task<bool> IsEmailTakenAsync(
        DataContext db,
        UserManager<User> userManager,
        string email,
        int exceptUserId = 0
    )
    {
        var normalizedEmail = userManager.NormalizeEmail(email);
        var normalizedUserName = userManager.NormalizeName(email);

        return db.Users.AnyAsync(x =>
            x.Id != exceptUserId
            && (x.NormalizedEmail == normalizedEmail || x.NormalizedUserName == normalizedUserName)
        );
    }

    private static void ApplyDisplayName(User user, string name, UserAccountResult result)
    {
        if (string.Equals(user.DisplayName, name, StringComparison.Ordinal))
            return;

        user.DisplayName = name;
        result.DisplayNameChanged = true;
    }

    /// <summary>Sets the e-mail and the user name together. Identity updates their normalized forms when saving.</summary>
    private static async Task ApplyEmailAsync(
        DataContext db,
        UserManager<User> userManager,
        User user,
        string email,
        UserAccountResult result,
        bool unchangedIsError
    )
    {
        // A user name that drifted from the e-mail counts as a change, so saving the same address repairs it.
        if (
            string.Equals(user.Email, email, StringComparison.Ordinal)
            && string.Equals(user.UserName, email, StringComparison.Ordinal)
        )
        {
            if (unchangedIsError)
                result.AddError(UserAccountField.Email, "This is already your email address.");

            return;
        }

        if (await IsEmailTakenAsync(db, userManager, email, user.Id))
        {
            result.AddError(UserAccountField.Email, EMAIL_TAKEN);
            return;
        }

        user.Email = email;
        user.UserName = email;
        // Nothing asks to confirm a changed address, and an unconfirmed account counts as an unfinished sign-up.
        user.EmailConfirmed = true;
        result.EmailChanged = true;
    }

    /// <summary>
    /// Checks the new password against the configured rules and sets it. Like <see cref="UserManager{TUser}"/>, it
    /// renews the security stamp, which ends sign-ins made with the old password.
    /// </summary>
    private static async Task ApplyPasswordAsync(
        UserManager<User> userManager,
        User user,
        string password,
        UserAccountResult result
    )
    {
        var errors = new List<IdentityError>();

        foreach (var validator in userManager.PasswordValidators)
            errors.AddRange((await validator.ValidateAsync(userManager, user, password)).Errors);

        if (errors.Count > 0)
        {
            AddIdentityErrors(errors, result);
            return;
        }

        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, password);
        user.SecurityStamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        result.PasswordChanged = true;
    }

    /// <remarks>Doesn't save anything, unlike <see cref="UserManager{TUser}.CheckPasswordAsync"/>.</remarks>
    private static bool IsCurrentPassword(UserManager<User> userManager, User user, string password) =>
        user.PasswordHash is not null
        && !string.IsNullOrEmpty(password)
        && userManager.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password)
            != PasswordVerificationResult.Failed;

    /// <summary>
    /// Generated avatars show the user's initials, so a new one is made after a name change. Uploaded pictures stay.
    /// </summary>
    private async Task RefreshGeneratedAvatarAsync(IServiceProvider services, User user)
    {
        if (!AvatarService.ShowsName || !AvatarService.IsGenerated(user.ProfilePictureFileName))
            return;

        // CleanupHostedService removes the old file once nothing points to it.
        user.ProfilePictureFileName =
            await TryGenerateAvatarAsync(services, user.DisplayName) ?? user.ProfilePictureFileName;
    }

    /// <summary>Returns null when the avatar can't be generated, e.g. while the avatar website is down.</summary>
    private async Task<string?> TryGenerateAvatarAsync(IServiceProvider services, string name)
    {
        try
        {
            return await services.GetRequiredService<AvatarService>().FetchAvatarAsync(name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not generate an avatar, so the account keeps its current one.");
            return null;
        }
    }

    private async Task SaveAsync(User user, UserAccountResult result, Func<Task<IdentityResult>> save)
    {
        try
        {
            AddIdentityErrors((await save()).Errors, result);
        }
        catch (DbUpdateException ex)
        {
            // E.g. another account took the same user name a moment ago and the unique index refused this one.
            logger.LogWarning(ex, "Saving user {UserId} failed.", user.Id);
            result.AddError(UserAccountField.General, "The changes couldn't be saved. Please try again.");
        }
    }

    /// <summary>Turns Identity's errors into ones attached to the field they are about.</summary>
    private static void AddIdentityErrors(IEnumerable<IdentityError> errors, UserAccountResult result)
    {
        foreach (var error in errors)
        {
            switch (error.Code)
            {
                case nameof(IdentityErrorDescriber.DuplicateUserName):
                case nameof(IdentityErrorDescriber.DuplicateEmail):
                    result.AddError(UserAccountField.Email, EMAIL_TAKEN);
                    break;
                // People never see their user name: it is their e-mail address.
                case nameof(IdentityErrorDescriber.InvalidUserName):
                    result.AddError(
                        UserAccountField.Email,
                        "This email address contains characters that can't be used for signing in."
                    );
                    break;
                case nameof(IdentityErrorDescriber.InvalidEmail):
                    result.AddError(UserAccountField.Email, error.Description);
                    break;
                case nameof(IdentityErrorDescriber.PasswordMismatch):
                    result.AddError(UserAccountField.CurrentPassword, WRONG_CURRENT_PASSWORD);
                    break;
                case nameof(IdentityErrorDescriber.ConcurrencyFailure):
                    result.AddError(
                        UserAccountField.General,
                        "This account was changed somewhere else in the meantime. Please try again."
                    );
                    break;
                default:
                    result.AddError(
                        error.Code.StartsWith("Password", StringComparison.Ordinal)
                            ? UserAccountField.NewPassword
                            : UserAccountField.General,
                        error.Description
                    );
                    break;
            }
        }
    }
}
