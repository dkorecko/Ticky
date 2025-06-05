namespace Ticky.Base.Entities;

public class Reminder : AbstractDbEntity
{
    public required DateTime At { get; set; }
    public required int CardId { get; set; }
    public virtual Card Card { get; set; } = null!;
}
