namespace Ticky.Base.Entities;

public class Attachment : AbstractDbEntity
{
    public required string FileName { get; set; }

    public required string OriginalName { get; set; }

    public required int CardId { get; set; }

    public virtual Card Card { get; set; } = null!;
}
