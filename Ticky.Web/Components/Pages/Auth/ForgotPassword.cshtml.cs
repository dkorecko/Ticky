namespace Ticky.Web.Pages.Auth;

public class ForgotPasswordModel : PageModel
{
    private readonly DataContext _dataContext;
    private readonly CodeService _codeService;
    private readonly MailService _mailService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public ForgotPasswordModel(
        DataContext dataContext,
        CodeService codeService,
        MailService mailService
    )
    {
        _dataContext = dataContext;
        _codeService = codeService;
        _mailService = mailService;
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
                await _mailService.SendForgottenPasswordCodeEmailAsync(
                    Input.EmailAddress,
                    await _codeService.CreateCodeAsync(targetUser, CodePurpose.ForgottenPassword)
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
