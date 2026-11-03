namespace Ticky.Base.Models;

/// <summary>An administrator's changes to a user. The password fields are optional: empty keeps the current one.</summary>
public class EditUserModel
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

    [Display(Name = "New password")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(Name = "Repeat new password")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords don't match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;

    /// <summary>When a new password is set, whether the user has to replace it at their next sign-in.</summary>
    public bool RequirePasswordChange { get; set; } = true;
}
