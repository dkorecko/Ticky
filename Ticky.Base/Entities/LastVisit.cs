namespace Ticky.Base.Entities;

public class LastVisit : AbstractDbEntity
{
    public required int BoardId { get; set; }
    public virtual Board Board { get; set; } = null!;

    public required DateTime VisitTime { get; set; }

    public required int UserId { get; set; }
    public virtual User User { get; set; } = null!;
}
