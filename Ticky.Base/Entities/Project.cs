namespace Ticky.Base.Entities;

public class Project : AbstractDbEntity, IDeletable
{
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }

    public virtual List<Board> Boards { get; set; } = [];

    public virtual List<ProjectMembership> Memberships { get; set; } = [];
}
