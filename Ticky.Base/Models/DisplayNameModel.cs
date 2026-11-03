namespace Ticky.Base.Models;

public class DisplayNameModel
{
    [Display(Name = "Display name")]
    [Required]
    [StringLength(Constants.Limits.MAX_DISPLAY_NAME_LENGTH, ErrorMessage = "The name can be at most {1} characters long.")]
    public string DisplayName { get; set; } = string.Empty;
}
