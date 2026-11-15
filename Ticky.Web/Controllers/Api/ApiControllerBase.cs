using System.Net.Mime;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// Base of the public API's controllers: API conventions (ProblemDetails errors, model validation), access for
/// signed-in users and API tokens, and the caller's user id.
/// </summary>
[ApiController]
[Authorize(Policy = Constants.Policies.ApiCallerAuthenticated)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, MediaTypeNames.Application.ProblemJson)]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// The caller's user id. The authorization policy only lets signed-in users and valid tokens through, and both
    /// carry it.
    /// </summary>
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
