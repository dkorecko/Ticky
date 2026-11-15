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
        if (ModelState.IsValid)
        {
            var targetUser = await _dataContext.Users.FirstOrDefaultAsync(x =>
                Input.EmailAddress.Equals(x.Email)
            );

            if (targetUser is not null)
            {
                await _emailService.SendForgottenPasswordCodeEmailAsync(
                    Input.EmailAddress,
                    await _codeService.CreateCodeAsync(targetUser, CodePurpose.ForgottenPassword)
                );
                return LocalRedirect("/auth/changepassword");
            }

            // An unknown address gets the same answer as a known one, so the form can't tell who has an account.
            return LocalRedirect("/auth/changepassword");
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
