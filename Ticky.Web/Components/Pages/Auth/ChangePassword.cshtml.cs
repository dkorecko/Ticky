namespace Ticky.Web.Pages.Auth;

public class ChangePasswordModel : PageModel
{
    private readonly DataContext _dataContext;
    private readonly UserManager<User> _userManager;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public ChangePasswordModel(DataContext dataContext, UserManager<User> userManager)
    {
        _dataContext = dataContext;
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
            var targetCode = await _dataContext
                .Codes.Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Value.Equals(Input.Code));

            if (targetCode is not null)
            {
                await _userManager.ResetPasswordAsync(
                    targetCode.User,
                    await _userManager.GeneratePasswordResetTokenAsync(targetCode.User),
                    Input.Password
                );
                _dataContext.Codes.Remove(targetCode);
                await _dataContext.SaveChangesAsync();
                return LocalRedirect("/auth/passwordchanged");
            }

            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.Code)}",
                "The provided code was not correct."
            );
        }

        return Page();
    }

    public class InputModel
    {
        [Required(AllowEmptyStrings = false)]
        [RegularExpression("[1-9]{3}-[1-9]{3}")]
        [Display(Name = "Confirmation code from the e-mail we sent you", Prompt = "123-456")]
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
