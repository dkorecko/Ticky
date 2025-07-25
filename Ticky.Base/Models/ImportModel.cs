namespace Ticky.Base.Models;

public class ImportModel
{
    public ImportSource Source { get; set; } = ImportSource.Trello;

    [Required(ErrorMessage = "There was no valid uploaded file.")]
    public TrelloImportDTO? ImportDto { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MinLength(1)]
    [MaxLength(5)]
    [RegularExpression("^[A-Z]*$", ErrorMessage = "The code must be in upper-case.")]
    public string Code { get; set; } = string.Empty;

    public string[]? MemberIdentifiers { get; set; }
}
