namespace Ticky.Web.Pages.Auth;

public class RegisterModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly EmailService _emailService;
    private readonly ILogger<RegisterModel> _logger;
    private readonly CodeService _codeService;
    private readonly AvatarService _avatarService;
    private readonly SignUpRateLimiter _signUpRateLimiter;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public RegisterModel(
        UserManager<User> userManager,
        EmailService emailService,
        ILogger<RegisterModel> logger,
        CodeService codeService,
        AvatarService avatarService,
        SignUpRateLimiter signUpRateLimiter
    )
    {
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
        _codeService = codeService;
        _avatarService = avatarService;
        _signUpRateLimiter = signUpRateLimiter;
    }

    /// <summary>
    /// Whether accounts can be created. Signing up needs an e-mail to confirm the address, and the administrator can
    /// turn it off.
    /// </summary>
    public bool SignUpsOpen => Constants.SMTP_ENABLED && !Constants.DISABLE_USER_SIGNUPS;

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // The page hides the form then, but a request doesn't need the form.
        if (!SignUpsOpen)
            return Page();

        if (ModelState.IsValid)
        {
            // This form says when an address is taken, so the number of attempts per client is limited.
            if (!_signUpRateLimiter.TryAcquire(HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty))
            {
                Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.Email)}",
                    $"Too many sign-up attempts. Please try again in {(int)Math.Ceiling(_signUpRateLimiter.Window.TotalMinutes)} minutes."
                );
                return Page();
            }

            Input.DisplayName = Input.DisplayName.Trim();
            Input.Email = Input.Email.Trim();

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
                Code code;

                try
                {
                    code = await _codeService.CreateCodeAsync(identity, CodePurpose.NewAccount);
                }
                catch
                {
                    // Without its sign-up code the account would count as a finished one, able to sign in.
                    await _userManager.DeleteAsync(identity);
                    throw;
                }

                try
                {
                    await _emailService.SendVerificationEmailAsync(Input.Email, code);
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
        [StringLength(
            Constants.Limits.MAX_DISPLAY_NAME_LENGTH,
            ErrorMessage = "The name can be at most {1} characters long."
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
