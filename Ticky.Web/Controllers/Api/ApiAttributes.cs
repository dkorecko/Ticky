using System.Net.Mime;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// Marks an endpoint that changes data: signed-in users and read and write tokens may call it, read-only tokens get
/// 403. The OpenAPI document lists that 403.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ApiWriteAttribute : AuthorizeAttribute, IApiResponseMetadataProvider
{
    /// <param name="tokenOnly">
    /// Only read and write tokens, not the browser's sign-in (see <see cref="Constants.Policies.ApiTokenCanWrite"/>).
    /// </param>
    public ApiWriteAttribute(bool tokenOnly = false) =>
        Policy = tokenOnly ? Constants.Policies.ApiTokenCanWrite : Constants.Policies.ApiCallerCanWrite;

    public Type Type => typeof(ProblemDetails);

    public int StatusCode => StatusCodes.Status403Forbidden;

    public void SetContentTypes(MediaTypeCollection contentTypes)
    {
        contentTypes.Clear();
        contentTypes.Add(MediaTypeNames.Application.ProblemJson);
    }
}

/// <summary>
/// Success responses are JSON, and the OpenAPI document lists only JSON for them. Unlike a plain
/// <see cref="ProducesAttribute"/>, it leaves errors alone, so they keep their <c>application/problem+json</c> type.
/// </summary>
public sealed class ProducesJsonAttribute() : ProducesAttribute(MediaTypeNames.Application.Json)
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: ProblemDetails })
            return;

        base.OnResultExecuting(context);
    }
}

/// <summary>An error response with a problem details body, for the OpenAPI document.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProblemResponseAttribute(int statusCode)
    : ProducesResponseTypeAttribute(
        statusCode == StatusCodes.Status400BadRequest ? typeof(ValidationProblemDetails) : typeof(ProblemDetails),
        statusCode,
        MediaTypeNames.Application.ProblemJson
    );

/// <summary>
/// Documents a 200 with what the action returns for API actions that declare no success response themselves. The
/// error responses every endpoint declares (see <see cref="ApiControllerBase"/>) would otherwise stop the OpenAPI
/// document from listing it.
/// </summary>
public sealed class DefaultSuccessResponseConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        if (!typeof(ApiControllerBase).IsAssignableFrom(action.Controller.ControllerType))
            return;

        // [Produces] counts as a 200 too, but without a type; it only sets the content type.
        var declared = action.Attributes
            .Concat(action.Controller.Attributes)
            .OfType<IApiResponseMetadataProvider>()
            .Where(x => x is not ProducesAttribute);

        if (declared.Any(x => x.StatusCode is >= 200 and < 300))
            return;

        // Without a type, the action's declared return type is used, e.g. CardDetailDTO for ActionResult<CardDetailDTO>.
        action.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status200OK));
    }
}
