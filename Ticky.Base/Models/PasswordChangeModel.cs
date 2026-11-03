namespace Ticky.Base.Models;

public class PasswordChangeModel
{
    [Display(Name = "Current password")]
    [Required]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Display(Name = "New password")]
    [Required]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(Name = "Repeat new password")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords don't match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
