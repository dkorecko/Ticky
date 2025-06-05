namespace Ticky.Base.Entities;

public class ProjectMembership : AbstractDbEntity
{
    public required int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public required int ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;

    public required bool IsAdmin { get; set; }

    public required DateTime AddedAt { get; set; }
}
