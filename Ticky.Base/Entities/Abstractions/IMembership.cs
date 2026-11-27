namespace Ticky.Base.Entities.Abstractions;

/// <summary>Someone's membership of a board or a project, and whether they administer it.</summary>
public interface IMembership : IDbEntry
{
    public int UserId { get; set; }

    public User User { get; set; }

    public bool IsAdmin { get; set; }

    public DateTime AddedAt { get; set; }
}
