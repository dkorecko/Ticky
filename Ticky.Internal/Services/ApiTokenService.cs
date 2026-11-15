using System.Security.Cryptography;
using System.Text;

namespace Ticky.Internal.Services;

/// <summary>
/// Creates, validates and revokes bearer tokens. The plaintext is shown to the user once at creation;
/// only the SHA-256 hash is stored, so a DB read gives an attacker nothing usable.
/// </summary>
public class ApiTokenService
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;

    public ApiTokenService(IDbContextFactory<DataContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public record CreateResult(ApiToken Token, string Plaintext);

    /// <summary>A presented token that is valid, with the user it acts as.</summary>
    public record ValidatedToken(User User, ApiTokenScope Scope);

    /// <exception cref="ArgumentException">The name is empty or longer than <see cref="Constants.Api.TokenNameMaxLength"/>.</exception>
    public async Task<CreateResult> CreateAsync(int userId, string name, ApiTokenScope scope, DateTime? expiresAt)
    {
        name = name?.Trim() ?? string.Empty;

        if (name.Length is 0 or > Constants.Api.TokenNameMaxLength)
            throw new ArgumentException(
                $"The name must be 1 to {Constants.Api.TokenNameMaxLength} characters long.",
                nameof(name)
            );

        if (!Enum.IsDefined(scope))
            throw new ArgumentOutOfRangeException(nameof(scope));

        var bytes = RandomNumberGenerator.GetBytes(Constants.Api.TokenRandomBytes);
        // Base64Url so the token fits unencoded in an Authorization header.
        var plaintext = Constants.Api.TokenPrefix + Base64UrlEncode(bytes);

        using var db = _dbContextFactory.CreateDbContext();

        var token = new ApiToken
        {
            UserId = userId,
            Name = name,
            TokenHash = HashToken(plaintext),
            Scope = scope,
            ExpiresAt = expiresAt,
        };

        db.ApiTokens.Add(token);
        await db.SaveChangesAsync();

        return new CreateResult(token, plaintext);
    }

    /// <summary>
    /// Looks up a presented token. Returns null if it is unknown, malformed, revoked or expired, or if its account is
    /// locked out or still has to replace the credentials it was given: until then the web app lets that account do
    /// nothing, and its tokens shouldn't be a way around that.
    /// Updates <see cref="ApiToken.LastUsedAt"/> on a hit, at most once per <see cref="Constants.Api.LastUsedPrecision"/>.
    /// </summary>
    public async Task<ValidatedToken?> ValidateAsync(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || !plaintext.StartsWith(Constants.Api.TokenPrefix, StringComparison.Ordinal))
            return null;

        var hash = HashToken(plaintext);
        var now = DateTime.Now;

        using var db = _dbContextFactory.CreateDbContext();

        var token = await db.ApiTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.TokenHash == hash && x.RevokedAt == null && (x.ExpiresAt == null || x.ExpiresAt > now)
            );

        if (token is null || token.User.NeedsNewCredentials || token.User.LockoutEnd > DateTimeOffset.UtcNow)
            return null;

        if (token.LastUsedAt is null || now - token.LastUsedAt >= Constants.Api.LastUsedPrecision)
        {
            token.LastUsedAt = now;
            await db.SaveChangesAsync();
        }

        return new ValidatedToken(token.User, token.Scope);
    }

    public async Task<List<ApiToken>> ListAsync(int userId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.ApiTokens
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    /// <returns>False if the token doesn't exist, belongs to someone else or is already revoked.</returns>
    public async Task<bool> RevokeAsync(int userId, int tokenId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var token = await db.ApiTokens
            .FirstOrDefaultAsync(x => x.Id == tokenId && x.UserId == userId);

        if (token is null || token.RevokedAt is not null)
            return false;

        token.RevokedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return true;
    }

    private static string HashToken(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
