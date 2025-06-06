namespace Ticky.Base.Models;

public class CredentialsModel
{
    [Display(Name = "Current e-mail address (username)")]
    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string OldEmailAddress { get; set; } = string.Empty;

    [Display(Name = "New e-mail address (username)")]
    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string NewEmailAddress { get; set; } = string.Empty;

    [Display(Name = "Current password")]
    [DataType(DataType.Password)]
    public string OldPassword { get; set; } = string.Empty;

    [Display(Name = "New password")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(Name = "Repeat new password")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword))]
    public string RepeatPassword { get; set; } = string.Empty;
}
