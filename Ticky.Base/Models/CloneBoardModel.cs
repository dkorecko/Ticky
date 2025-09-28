namespace Ticky.Base.Models;

public class CloneBoardModel
{
    [Display(Name = "Target project")]
    [Required]
    public int? TargetProjectId { get; set; }

    [Display(Name = "Board code/identifier")]
    [Required(AllowEmptyStrings = false)]
    [MinLength(1)]
    [MaxLength(5)]
    [RegularExpression("^[A-Z]*$", ErrorMessage = "The code must be in upper-case.")]
    public string Code { get; set; } = string.Empty;
}
