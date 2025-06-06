namespace Ticky.Base.Models;

public class UserModel
{
    [Display(Name = "Full name")]
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [Display(Name = "Email address")]
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Password")]
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Repeat password")]
    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
