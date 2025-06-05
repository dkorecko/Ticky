namespace Ticky.Web.Pages.Auth;

public class ConfirmMailModel : PageModel
{
    private readonly DataContext _dataContext;
    private readonly SignInManager<User> _signInManager;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public ConfirmMailModel(DataContext dataContext, SignInManager<User> signInManager)
    {
        _dataContext = dataContext;
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
            var targetUser = await _dataContext
                .Users.Where(x => Input.EmailAddress.Equals(x.Email))
                .Include(x => x.EmailVerificationCode)
                .FirstOrDefaultAsync(x =>
                    x.EmailVerificationCode != null
                    && Input.Code.Equals(x.EmailVerificationCode.Value)
                );

            if (targetUser is not null)
            {
                targetUser.EmailConfirmed = true;
                _dataContext.Update(targetUser);
                _dataContext.Codes.Remove(targetUser.EmailVerificationCode!);

                await _dataContext.SaveChangesAsync();

                await _signInManager.SignInAsync(targetUser, true);

                return LocalRedirect("/auth/mailconfirmed");
            }

            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.Code)}",
                "The provided code was not valid."
            );
        }

        return Page();
    }

    public class InputModel
    {
        [Required(AllowEmptyStrings = false)]
        [RegularExpression("[1-9]{3}-[1-9]{3}")]
        [Display(Name = "Confirmation code from the received e-mail", Prompt = "123-456")]
        public string Code { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string EmailAddress { get; set; } = string.Empty;
    }
}
