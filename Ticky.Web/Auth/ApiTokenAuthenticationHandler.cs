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
        if (!Request.Headers.TryGetValue("Authorization", out var header))
            return AuthenticateResult.NoResult();

        var value = header.ToString();
        const string prefix = "Bearer ";

        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = value[prefix.Length..].Trim();

        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        var user = await _apiTokenService.ValidateAsync(token);

        if (user is null)
            return AuthenticateResult.Fail("Invalid or revoked API token.");

        // Match the Identity cookie's claim shape so downstream code can use
        // User.FindFirst(ClaimTypes.NameIdentifier) exactly as before.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.DisplayName),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
