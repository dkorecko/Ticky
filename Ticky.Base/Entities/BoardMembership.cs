namespace Ticky.Base.Entities;

public class BoardMembership : AbstractDbEntity
{
    public required int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public required int BoardId { get; set; }
    public virtual Board Board { get; set; } = null!;

    public required bool IsAdmin { get; set; }

    public required DateTime AddedAt { get; set; }
}
