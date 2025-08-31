using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Ticky.Web.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly SignInManager<User> _signInManager;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public LoginModel(SignInManager<User> signInManager)
    {
        _signInManager = signInManager;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(
                Input.Email,
                Input.Password,
                true,
                lockoutOnFailure: false
            );

            if (result.Succeeded)
                return LocalRedirect("~/");
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
