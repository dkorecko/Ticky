namespace Ticky.Base.Entities.Abstractions;

public interface IDbEntry
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; }
}
