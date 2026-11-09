namespace Ticky.Base.Entities;

/// <summary>
/// A long-lived bearer token minted by a user for programmatic access. Carries the user's identity
/// and permissions one-for-one — there are no separate scopes yet. The token's plaintext is shown
/// to the user once at mint time and never again; only a SHA-256 hash is stored.
/// </summary>
public class ApiToken : AbstractDbEntity
{
    public required int UserId { get; set; }

    public virtual User User { get; set; } = null!;

    /// <summary>User-supplied label, for telling tokens apart in the UI (e.g. "openclaw-grouper").</summary>
    [Required(AllowEmptyStrings = false)]
    [MaxLength(80)]
    public required string Name { get; set; }

    /// <summary>SHA-256 of the token plaintext, hex-encoded. Indexed for lookup.</summary>
    [Required]
    [MaxLength(64)]
    public required string TokenHash { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
