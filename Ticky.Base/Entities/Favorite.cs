namespace Ticky.Base.Entities;

public class Favorite : AbstractDbEntity
{
    public required int UserId { get; set; }

    public virtual User User { get; set; } = null!;

    public required int BoardId { get; set; }

    public virtual Board Board { get; set; } = null!;
}
