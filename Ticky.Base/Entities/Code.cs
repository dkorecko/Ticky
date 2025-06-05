namespace Ticky.Base.Entities;

public class Code : AbstractDbEntity
{
    public required string Value { get; set; }

    public required CodePurpose CodePurpose { get; set; }

    public required int UserId { get; set; }

    public virtual User User { get; set; } = default!;
}
