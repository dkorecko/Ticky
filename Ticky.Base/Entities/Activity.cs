namespace Ticky.Base.Entities;

public class Activity : AbstractDbEntity
{
    public required int UserId { get; set; }
    public virtual User User { get; set; } = null!;
    public required string Text { get; set; }
    public required DateTime At { get; set; }
    public required int CardId { get; set; }
    public virtual Card Card { get; set; } = null!;
}
