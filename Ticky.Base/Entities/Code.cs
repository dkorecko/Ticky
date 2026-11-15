namespace Ticky.Base.Entities;

public class Code : AbstractDbEntity
{
    public required string Value { get; set; }

    public required CodePurpose CodePurpose { get; set; }

    public required int UserId { get; set; }

    /// <summary>How many wrong guesses were made at this code. The code is deleted once the limit is reached.</summary>
    public int FailedAttempts { get; set; }

    public virtual User User { get; set; } = default!;
}
