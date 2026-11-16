using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Ticky.Web.Auth;

/// <summary>
/// Refuses the sign-in of an account whose sign-up isn't finished (see <see cref="UnfinishedSignUpHelper"/>).
/// Identity's <c>RequireConfirmedEmail</c> isn't an option because it would lock out every account that never
/// confirmed an address, like those without one or created before confirming existed.
/// </summary>
/// <remarks>
/// The check runs after the password was verified, so it only tells someone who knows the password that the sign-up
/// is unfinished. <see cref="SignInManager{TUser}.SignInAsync(TUser, bool, string?)"/>, which the confirmation page
/// uses to sign the user in once the address is confirmed, isn't affected.
/// </remarks>
public class SignUpAwareSignInManager(
    UserManager<User> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<User> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<User>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<User> confirmation
)
    : SignInManager<User>(
        userManager,
        contextAccessor,
        claimsFactory,
        optionsAccessor,
        logger,
        schemes,
        confirmation
    )
{
    protected override async Task<SignInResult> SignInOrTwoFactorAsync(
        User user,
        bool isPersistent,
        string? loginProvider = null,
        bool bypassTwoFactor = false
    )
    {
        if (await UserManager.Users.WithUnfinishedSignUp().AnyAsync(x => x.Id == user.Id))
            return SignInResult.NotAllowed;

        return await base.SignInOrTwoFactorAsync(user, isPersistent, loginProvider, bypassTwoFactor);
    }
}
