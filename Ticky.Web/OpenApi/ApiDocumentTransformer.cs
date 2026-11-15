using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Ticky.Web.OpenApi;

/// <summary>
/// Describes the API as a whole in the OpenAPI document: what it is, the rules that apply to every endpoint,
/// and the bearer token every endpoint takes.
/// </summary>
public class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string DESCRIPTION = """
        Read and change your Ticky boards from scripts and agents. Every request acts as the user who created the
        token, so it reaches exactly the boards that user can.

        **Authentication.** Create a token in Ticky under *Settings → API tokens* and send it with every request as
        `Authorization: Bearer tky_…`. Read-only tokens can call the `GET` endpoints; the others need a read and
        write token and answer `403` otherwise.

        **Conventions.**
        - Times are in the server's local time and are returned without an offset. Times you send without an
          offset are taken as server time; times with an offset (or `Z`) are converted to it.
        - Enums are sent and returned as names, e.g. `"High"`. Numbers are rejected.
        - Errors are [problem details](https://www.rfc-editor.org/rfc/rfc9457) with `status`, `title` and, where
          useful, `detail` or a per-field `errors` object.
        - Boards and cards you can't access answer `404`, the same as ones that don't exist.
        """;

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Info = new OpenApiInfo
        {
            Title = "Ticky API",
            Version = Constants.Api.Version,
            Description = DESCRIPTION,
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[Constants.Api.SecuritySchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "An API token from Settings → API tokens, starting with `tky_`.",
        };

        document.Security ??= [];
        document.Security.Add(
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(Constants.Api.SecuritySchemeName, document)] = [],
            }
        );

        return Task.CompletedTask;
    }
}
