namespace Ticky.Base.Entities;

public class Board : AbstractDbEntity
{
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }
    public required string Description { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MinLength(1)]
    [MaxLength(5)]
    [RegularExpression("^[A-Z]*$", ErrorMessage = "The code must be in upper-case.")]
    public required string Code { get; set; }
    public required int ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;
    public virtual List<Column> Columns { get; set; } = [];
    public virtual List<BoardMembership> Memberships { get; set; } = [];
    public virtual List<Label> Labels { get; set; } = [];
    public virtual List<LastVisit> LastVisits { get; set; } = [];
    public virtual List<Favorite> Favorites { get; set; } = [];
}
