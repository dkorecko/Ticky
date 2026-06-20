namespace Ticky.Base.Entities;

public class CardColumnHistory : AbstractDbEntity
{
    public required int CardId { get; set; }

    public int? FromColumnId { get; set; }
    public required string FromColumnName { get; set; }

    public int? ToColumnId { get; set; }
    public required string ToColumnName { get; set; }
    public required bool ToColumnFinished { get; set; }

    public int? MovedByUserId { get; set; }

    public virtual Card Card { get; set; } = null!;
    public virtual Column? FromColumn { get; set; }
    public virtual Column? ToColumn { get; set; }
    public virtual User? MovedBy { get; set; }
}
