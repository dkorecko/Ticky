namespace Ticky.Internal.Helpers;

/// <summary>
/// An account whose sign-up isn't finished: its e-mail address is unconfirmed and the code that confirms it is still
/// waiting. Such an account can't sign in until the address is confirmed, and the cleanup deletes it once the code
/// expires. Accounts that were never confirmed but have no such code (made before confirming existed, or without an
/// e-mail address) are in use and aren't affected.
/// </summary>
public static class UnfinishedSignUpHelper
{
    /// <summary>Only the accounts that still wait for their e-mail address to be confirmed.</summary>
    public static IQueryable<User> WithUnfinishedSignUp(this IQueryable<User> users) =>
        users.Where(x =>
            !x.EmailConfirmed
            && x.EmailVerificationCode != null
            && x.EmailVerificationCode.CodePurpose == CodePurpose.NewAccount
        );
}
