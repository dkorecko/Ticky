namespace Ticky.Base.Entities;

public class Comment : AbstractDbEntity
{
    public required string Text { get; set; }
    public required int CreatedById { get; set; }
    public virtual User CreatedBy { get; set; } = null!;
    public required int CardId { get; set; }
    public virtual Card Card { get; set; } = null!;
}
