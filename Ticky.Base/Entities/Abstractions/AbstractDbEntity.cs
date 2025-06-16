namespace Ticky.Base.Entities.Abstractions;

public abstract class AbstractDbEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.Now;
}
