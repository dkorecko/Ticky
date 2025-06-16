namespace Ticky.Base.Entities.Abstractions;

public interface IDeletable : IDbEntry
{
    public string Name { get; }
}
