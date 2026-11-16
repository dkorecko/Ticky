namespace Ticky.Web.Pages.Auth;

public class ForgotPasswordModel : PageModel
{
    private readonly DataContext _dataContext;
    private readonly CodeService _codeService;
    private readonly EmailService _emailService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public ForgotPasswordModel(
        DataContext dataContext,
        CodeService codeService,
        EmailService emailService
    )
    {
        _dataContext = dataContext;
        _codeService = codeService;
        _emailService = emailService;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Without SMTP nobody could receive the code. The page doesn't offer the form either.
        if (!Constants.SMTP_ENABLED)
            return Page();

        if (ModelState.IsValid)
        {
            var targetUser = await _dataContext.Users.FirstOrDefaultAsync(x =>
                Input.EmailAddress.Equals(x.Email)
            );

            // A sign-up that isn't confirmed yet is handled like an unknown account, its code must stay in place.
            if (targetUser is not null && !await _codeService.HasPendingSignUpAsync(targetUser))
            {
                // Null when a code was mailed a moment ago: it stays valid and isn't replaced or mailed again.
                var code = await _codeService.TryCreateCodeAsync(
                    targetUser,
                    CodePurpose.ForgottenPassword
                );

                if (code is not null)
                    await _emailService.SendForgottenPasswordCodeEmailAsync(
                        Input.EmailAddress,
                        code
                    );

                return LocalRedirect("/auth/changepassword");
            }

            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.EmailAddress)}",
                "The provider e-mail address is not linked to any existing account."
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
    }
}
