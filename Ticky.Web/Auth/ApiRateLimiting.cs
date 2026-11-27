using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ticky.Web.Auth;

/// <summary>
/// Limits how many API requests each user makes a minute (<see cref="Constants.API_RATE_LIMIT"/>), so one runaway
/// script or agent can't swamp a self-hosted instance. Callers past the limit get 429 with a <c>Retry-After</c>.
/// </summary>
public static class ApiRateLimiting
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new()
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Title = "Too Many Requests",
                        Detail = "Too many requests. Wait as long as Retry-After says, then try again.",
                    },
                });
            };

            options.AddPolicy(Constants.Api.RateLimitPolicy, context =>
                Constants.API_RATE_LIMIT <= 0
                    ? RateLimitPartition.GetNoLimiter(string.Empty)
                    : RateLimitPartition.GetFixedWindowLimiter(
                        PartitionKey(context),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = Constants.API_RATE_LIMIT,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }
                    )
            );
        });

    /// <summary>
    /// Who the request counts against: the user the endpoint's authorization signed in, by token or cookie, so all of
    /// a user's tokens share one limit. Nothing reaches an API endpoint unauthenticated, but the address is the
    /// fallback.
    /// </summary>
    private static string PartitionKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
            ? "user:" + userId
            : "address:" + context.Connection.RemoteIpAddress;
}
