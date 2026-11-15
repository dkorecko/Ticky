namespace Ticky.Web.Pages.Auth;

public class ConfirmMailModel : PageModel
{
    private readonly DataContext _dataContext;
    private readonly CodeService _codeService;
    private readonly SignInManager<User> _signInManager;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public ConfirmMailModel(
        DataContext dataContext,
        CodeService codeService,
        SignInManager<User> signInManager
    )
    {
        _dataContext = dataContext;
        _codeService = codeService;
        _signInManager = signInManager;
    }

    public IActionResult OnGet(string email)
    {
        Input.EmailAddress = email;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var targetCode = await _codeService.VerifyCodeAsync(
                Input.EmailAddress,
                Input.Code,
                CodePurpose.NewAccount
            );

            if (targetCode is not null)
            {
                var targetUser = targetCode.User;
                targetUser.EmailConfirmed = true;
                await _dataContext.SaveChangesAsync();
                await _codeService.ConsumeCodeAsync(targetCode);

                await _signInManager.SignInAsync(targetUser, true);

                return LocalRedirect("/auth/mailconfirmed");
            }

            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.Code)}",
                "The e-mail address or the code was not correct, or the code has expired."
            );
        }

        return Page();
    }

    public class InputModel
    {
        [Required(AllowEmptyStrings = false)]
        [RegularExpression(CodeService.InputPattern)]
        [Display(Name = "Confirmation code from the received e-mail", Prompt = "K7M2-QX4P")]
        public string Code { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string EmailAddress { get; set; } = string.Empty;
    }
}
