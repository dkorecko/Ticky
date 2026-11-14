using System.Security.Cryptography;
using System.Text;

namespace Ticky.Internal.Services;

/// <summary>
/// Mints, validates and revokes long-lived bearer tokens. The plaintext is shown to the user once
/// at mint time; only the SHA-256 hash is stored, so a DB read gives an attacker nothing usable.
/// </summary>
public class ApiTokenService
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;

    public ApiTokenService(IDbContextFactory<DataContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public record MintResult(ApiToken Token, string Plaintext);

    public async Task<MintResult> MintAsync(int userId, string name)
    {
        var bytes = RandomNumberGenerator.GetBytes(Constants.Api.TokenRandomBytes);
        // Base64Url so the token fits unencoded in an Authorization header.
        var plaintext = Constants.Api.TokenPrefix + Base64UrlEncode(bytes);
        var hash = HashToken(plaintext);

        using var db = _dbContextFactory.CreateDbContext();

        var token = new ApiToken
        {
            UserId = userId,
            Name = name,
            TokenHash = hash,
        };

        db.ApiTokens.Add(token);
        await db.SaveChangesAsync();

        return new MintResult(token, plaintext);
    }

    /// <summary>
    /// Look up and authenticate a presented token. Returns the user it belongs to, or null if the token
    /// is unknown, malformed or revoked. Updates <see cref="ApiToken.LastUsedAt"/> on a hit.
    /// </summary>
    public async Task<User?> ValidateAsync(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || !plaintext.StartsWith(Constants.Api.TokenPrefix))
            return null;

        var hash = HashToken(plaintext);

        using var db = _dbContextFactory.CreateDbContext();

        var token = await db.ApiTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAt == null);

        if (token is null)
            return null;

        token.LastUsedAt = DateTime.Now;
        await db.SaveChangesAsync();

        return token.User;
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
