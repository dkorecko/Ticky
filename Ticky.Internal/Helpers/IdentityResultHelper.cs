using Microsoft.AspNetCore.Identity;

namespace Ticky.Internal.Helpers;

public static class IdentityResultHelper
{
    private static readonly string[] DuplicateAccountCodes =
    [
        nameof(IdentityErrorDescriber.DuplicateUserName),
        nameof(IdentityErrorDescriber.DuplicateEmail)
    ];

    /// <summary>
    /// Whether the account wasn't created because one with that e-mail address already exists. Identity checks the
    /// password first, so a rejected password never comes with this error.
    /// </summary>
    public static bool IsDuplicateAccount(this IdentityResult result) =>
        !result.Succeeded && result.Errors.Any(x => DuplicateAccountCodes.Contains(x.Code));
}
