namespace Ticky.Base.Models;

/// <summary>A new user created by an administrator.</summary>
public class UserModel
{
    [Display(Name = "Full name")]
    [Required]
    [StringLength(Constants.Limits.MAX_DISPLAY_NAME_LENGTH, ErrorMessage = "The name can be at most {1} characters long.")]
    public string DisplayName { get; set; } = string.Empty;

    [Display(Name = "Email address")]
    [Required]
    [EmailAddress]
    [StringLength(Constants.Limits.MAX_EMAIL_LENGTH, ErrorMessage = "The email address can be at most {1} characters long.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Password")]
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Repeat password")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
