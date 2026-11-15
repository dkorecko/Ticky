namespace Ticky.Base.Entities;

/// <summary>
/// A bearer token a user creates for programmatic access. It acts as that user, limited by its
/// <see cref="Scope"/>. The token's plaintext is shown to the user once at creation and never again;
/// only a SHA-256 hash is stored.
/// </summary>
public class ApiToken : AbstractDbEntity
{
    public required int UserId { get; set; }

    public virtual User User { get; set; } = null!;

    /// <summary>User-supplied label, for telling tokens apart in the UI (e.g. "Claude Code").</summary>
    [Required(AllowEmptyStrings = false)]
    [MaxLength(Constants.Api.TokenNameMaxLength)]
    public required string Name { get; set; }

    /// <summary>SHA-256 of the token plaintext, hex-encoded. Indexed for lookup.</summary>
    [Required]
    [MaxLength(64)]
    public required string TokenHash { get; set; }

    public ApiTokenScope Scope { get; set; }

    /// <summary>When the token stops working, or null if it never expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Whether the token is neither revoked nor expired at <paramref name="now"/>.</summary>
    public bool IsActiveAt(DateTime now) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
}
