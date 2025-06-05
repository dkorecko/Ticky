namespace Ticky.Base.Entities;

public abstract class AbstractDbEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.Now;
}
