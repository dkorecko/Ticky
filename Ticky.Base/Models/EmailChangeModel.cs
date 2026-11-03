namespace Ticky.Base.Models;

public class EmailChangeModel
{
    [Display(Name = "New email address")]
    [Required]
    [EmailAddress]
    [StringLength(Constants.Limits.MAX_EMAIL_LENGTH, ErrorMessage = "The email address can be at most {1} characters long.")]
    public string NewEmail { get; set; } = string.Empty;

    [Display(Name = "Current password")]
    [Required]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;
}
