using Microsoft.AspNetCore.Identity;

namespace Ticky.Internal.Data;

/// <summary>
/// Gives accounts whose user name no longer matches their e-mail that e-mail as their user name. Editing a user in
/// the admin panel used to change only the e-mail, and since people sign in with their user name, they could no longer
/// sign in with the address shown for them.
/// </summary>
/// <remarks>
/// Safe to run on every start: it only looks at accounts whose user name and e-mail differ. An account whose e-mail
/// is another account's user name is left alone and logged, since only a person can tell which one should get it.
/// </remarks>
public static class UserNameEmailSync
{
    public static async Task RunAsync(DataContext dataContext, UserManager<User> userManager, ILogger logger)
    {
        var mismatched = await dataContext
            .Users.Where(x => x.Email != null && x.NormalizedEmail != x.NormalizedUserName)
            .ToListAsync();

        foreach (var user in mismatched)
        {
            var normalizedUserName = userManager.NormalizeName(user.Email);
            var takenBy = await dataContext
                .Users.Where(x => x.Id != user.Id && x.NormalizedUserName == normalizedUserName)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();

            if (takenBy is not null)
            {
                logger.LogWarning(
                    "User {UserId} can't sign in with its e-mail address, because user {OtherUserId} has it as its user name. Give one of them a different e-mail address in the admin panel.",
                    user.Id,
                    takenBy
                );
                continue;
            }

            user.UserName = user.Email;
            var result = await userManager.UpdateAsync(user);

            if (result.Succeeded)
                logger.LogInformation(
                    "User {UserId} can now sign in with its e-mail address again: its user name was changed to match it.",
                    user.Id
                );
            else
                logger.LogWarning(
                    "Could not change the user name of user {UserId} to match its e-mail address: {Errors}",
                    user.Id,
                    string.Join(" ", result.Errors.Select(x => x.Description))
                );
        }
    }
}
