namespace Ticky.Base.Entities;

public interface IDbEntry
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; }
}
