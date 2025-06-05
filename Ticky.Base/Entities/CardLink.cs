namespace Ticky.Base.Entities;

public class CardLink : AbstractDbEntity
{
    public required int CardOneId { get; set; }

    public virtual Card CardOne { get; set; } = null!;

    public required int CardTwoId { get; set; }

    public virtual Card CardTwo { get; set; } = null!;

    public required string Category { get; set; }
}
