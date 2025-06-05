namespace Ticky.Web.Pages.Auth;

public class RegisterModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly MailService _mailService;
    private readonly ILogger<RegisterModel> _logger;
    private readonly CodeService _codeService;
    private readonly AvatarService _avatarService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public RegisterModel(
        UserManager<User> userManager,
        MailService mailService,
        ILogger<RegisterModel> logger,
        CodeService codeService,
        AvatarService avatarService
    )
    {
        _userManager = userManager;
        _mailService = mailService;
        _logger = logger;
        _codeService = codeService;
        _avatarService = avatarService;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var identity = new User
            {
                DisplayName = Input.DisplayName,
                UserName = Input.Email,
                Email = Input.Email,
                ProfilePictureFileName = await _avatarService.FetchAvatarAsync(Input.DisplayName)
            };

            var result = await _userManager.CreateAsync(identity, Input.Password);

            if (result.Succeeded)
            {
                var code = await _codeService.CreateCodeAsync(identity, CodePurpose.NewAccount);

                try
                {
                    await _mailService.SendVerificationEmailAsync(Input.Email, code);
                    return LocalRedirect($"/auth/confirmmail?email={Input.Email}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"Received an exception when sending an e-mail: {ex}. User received information about providing invalid e-mail."
                    );
                    ModelState.AddModelError(
                        $"{nameof(Input)}.{nameof(Input.Email)}",
                        "The provided e-mail address does not exist."
                    );

                    await _userManager.DeleteAsync(identity);
                }
            }
            else
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        $"{nameof(Input)}.{nameof(Input.Password)}",
                        error.Description
                    );
                }
        }

        return Page();
    }

    public class InputModel
    {
        [Required(AllowEmptyStrings = false)]
        [RegularExpression(
            "^[A-z1-9\\sá-žÁ-Ž-]*$",
            ErrorMessage = "The name must not use any special characters."
        )]
        [Display(Name = "Name to represent you", Prompt = "John Doe")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        [EmailAddress]
        [Display(Name = "E-mail address", Prompt = "sample@email.com")]
        public string Email { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        [DataType(DataType.Password)]
        [Display(Name = "Password", Prompt = "●●●●●●●●●●●")]
        public string Password { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        [DataType(DataType.Password)]
        [Display(Name = "Repeat password", Prompt = "●●●●●●●●●●●")]
        [Compare("Password", ErrorMessage = "The passwords must match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
