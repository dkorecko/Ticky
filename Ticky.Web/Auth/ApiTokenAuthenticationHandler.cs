using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Ticky.Web.Auth;

public class ApiTokenAuthenticationOptions : AuthenticationSchemeOptions { }

/// <summary>
/// Reads an <c>Authorization: Bearer tky_…</c> header and resolves it to the owning user via
/// <see cref="ApiTokenService"/>. Populates the same claim shape the Identity cookie produces so
/// existing authorization policies (board-membership checks etc.) see no difference between a
/// browser session and an API call.
/// </summary>
public class ApiTokenAuthenticationHandler
    : AuthenticationHandler<ApiTokenAuthenticationOptions>
{
    /// <summary>The HTTP authentication scheme of the Authorization and WWW-Authenticate headers (RFC 6750).</summary>
    private const string BearerScheme = "Bearer";

    private readonly ApiTokenService _apiTokenService;

    public ApiTokenAuthenticationHandler(
        IOptionsMonitor<ApiTokenAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiTokenService apiTokenService
    )
        : base(options, logger, encoder)
    {
        _apiTokenService = apiTokenService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken();

        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        var validated = await _apiTokenService.ValidateAsync(token);

        if (validated is null)
            return AuthenticateResult.Fail("Invalid, expired or revoked API token.");

        var user = validated.User;

        // Match the Identity cookie's claim shape so downstream code can use
        // User.FindFirst(ClaimTypes.NameIdentifier) exactly as before. The scope claim only exists for
        // token callers; the ApiCallerCanWrite policy reads it.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.DisplayName),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(Constants.Api.ScopeClaim, validated.Scope.ToString()),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// The token from the <c>Authorization: Bearer</c> header. Connections to the live-update hub may send it as the
    /// <c>access_token</c> query parameter instead, as SignalR clients do where they can't set headers.
    /// </summary>
    private string? ReadToken()
    {
        var header = Request.Headers.Authorization.ToString();
        const string prefix = BearerScheme + " ";

        if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return header[prefix.Length..].Trim();

        if (Request.Path.StartsWithSegments(Constants.Hubs.UPDATE_HUB))
            return Request.Query[Constants.Api.HubTokenParameter].ToString().Trim();

        return null;
    }

    /// <summary>Tells clients a 401 wants a bearer token, as HTTP expects of a 401.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = BearerScheme;
        return base.HandleChallengeAsync(properties);
    }
}
