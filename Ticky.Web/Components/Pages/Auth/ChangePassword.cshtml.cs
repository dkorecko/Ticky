namespace Ticky.Web.Pages.Auth;

public class ChangePasswordModel : PageModel
{
    private readonly CodeService _codeService;
    private readonly UserManager<User> _userManager;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public ChangePasswordModel(CodeService codeService, UserManager<User> userManager)
    {
        _codeService = codeService;
        _userManager = userManager;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var targetCode = await _codeService.VerifyCodeAsync(
                Input.EmailAddress,
                Input.Code,
                CodePurpose.ForgottenPassword
            );

            if (targetCode is not null)
            {
                var result = await _userManager.ResetPasswordAsync(
                    targetCode.User,
                    await _userManager.GeneratePasswordResetTokenAsync(targetCode.User),
                    Input.Password
                );

                if (result.Succeeded)
                {
                    await _codeService.ConsumeCodeAsync(targetCode);
                    return LocalRedirect("/auth/passwordchanged");
                }

                // The code stays valid, so a password the rules don't allow can be replaced by another one.
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        $"{nameof(Input)}.{nameof(Input.Password)}",
                        error.Description
                    );
                }

                return Page();
            }

            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.Code)}",
                Constants.Codes.REJECTED_MESSAGE
            );
        }

        return Page();
    }

    public class InputModel
    {
        [Display(Name = "E-mail address of your account", Prompt = "email@gmail.com")]
        [Required(AllowEmptyStrings = false)]
        [EmailAddress]
        public string EmailAddress { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        [RegularExpression(Constants.Codes.INPUT_PATTERN)]
        [Display(Name = "Confirmation code from the e-mail we sent you", Prompt = Constants.Codes.EXAMPLE)]
        public string Code { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        [DataType(DataType.Password)]
        [Display(Name = "New password", Prompt = "●●●●●●●●●●●")]
        public string Password { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        [DataType(DataType.Password)]
        [Display(Name = "Repeat new password", Prompt = "●●●●●●●●●●●")]
        [Compare("Password", ErrorMessage = "The passwords must match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
