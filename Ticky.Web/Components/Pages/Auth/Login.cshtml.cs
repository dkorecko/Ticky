using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Ticky.Web.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly SignInManager<User> _signInManager;
    private readonly int _lockoutMinutes;

    [BindProperty]
    public InputModel Input { get; set; } = new();
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>The user was signed out because their password changed.</summary>
    public bool PasswordChanged { get; set; }

    public LoginModel(SignInManager<User> signInManager, IOptions<IdentityOptions> identityOptions)
    {
        _signInManager = signInManager;
        _lockoutMinutes = (int)Math.Ceiling(identityOptions.Value.Lockout.DefaultLockoutTimeSpan.TotalMinutes);
    }

    public IActionResult OnGet(string? returnUrl = null, bool passwordChanged = false)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        PasswordChanged = passwordChanged;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (ModelState.IsValid)
        {
            // Keyboards and autofill like to add a space, which would make the address not match.
            var result = await _signInManager.PasswordSignInAsync(
                Input.Email.Trim(),
                Input.Password,
                true,
                lockoutOnFailure: true
            );

            if (result.Succeeded)
                return LocalRedirect(returnUrl ?? Url.Content("~/"));
            else if (result.IsLockedOut)
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.Email)}",
                    $"Too many failed sign-in attempts. Please try again in {_lockoutMinutes} minutes."
                );
            else
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.Email)}",
                    "The provided e-mail/password combination does not exist."
                );
        }

        return Page();
    }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "E-mail address", Prompt = "sample@email.com")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password", Prompt = "●●●●●●●●●●●")]
        public string Password { get; set; } = string.Empty;
    }
}
