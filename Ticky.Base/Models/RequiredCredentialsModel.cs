namespace Ticky.Base.Models;

/// <summary>
/// The credentials a user must set before using the app, when their account still has the default credentials or
/// ones an administrator chose for them.
/// </summary>
public class RequiredCredentialsModel
{
    [Display(Name = "Email address")]
    [Required]
    [EmailAddress]
    [StringLength(Constants.Limits.MAX_EMAIL_LENGTH, ErrorMessage = "The email address can be at most {1} characters long.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "New password")]
    [Required]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(Name = "Repeat new password")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords don't match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
