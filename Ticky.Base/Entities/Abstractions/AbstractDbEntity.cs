namespace Ticky.Base.Entities.Abstractions;

public abstract class AbstractDbEntity : IDbEntry
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.Now;
}
