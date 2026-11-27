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
        Read and change your Ticky projects, boards and cards from scripts and agents: everything you can do in the
        app, under the same rules. Every request acts as the user who created the token, so it reaches exactly the
        boards that user can.

        **Authentication.** Create a token in Ticky under *Settings → API tokens* and send it with every request as
        `Authorization: Bearer tky_…`. Read-only tokens can call the `GET` endpoints; the others need a read and
        write token and answer `403` otherwise.

        **Getting around.** `GET /me` says who you are. `GET /boards` lists your boards; `GET /boards/{id}` gives a
        board's columns, labels and members, whose ids the card endpoints take. People know cards by keys like
        `TKY-12`; `GET /search?q=TKY-12` finds one.

        **Permissions.** Members of a board, or of its project, can do everything with its cards. Board admins also
        change the board, its columns, labels and members; project admins change the project and its members and add
        boards to it. Anyone can create a project.

        **Conventions.**
        - Times are in the server's local time and are returned without an offset. Times you send without an
          offset are taken as server time; times with an offset (or `Z`) are converted to it. Durations are
          `hh:mm:ss`.
        - Enums are sent and returned as names, e.g. `"High"`. Numbers are rejected.
        - Partial updates (`PATCH`) change only the fields you send. Adding something that's already there, or
          removing something that isn't, changes nothing and succeeds.
        - Errors are [problem details](https://www.rfc-editor.org/rfc/rfc9457) with `status`, `title` and, where
          useful, `detail` or a per-field `errors` object.
        - Anything you can't access answers `404`, the same as something that doesn't exist. `403` means you can
          see it but may not make that change.
        - Activity is paged, newest first: pass a page's `nextBefore` as `before` for the next one.
        - Each user may make a limited number of requests a minute, across all their tokens (600 unless the
          instance sets `API_RATE_LIMIT`); past it, requests get `429` with a `Retry-After` header.

        **Live updates.** Changes show up on open boards right away and in the card's activity as yours. To hear
        about changes yourself, connect a SignalR client to `/updatehub` with your token (as the `access_token`
        query parameter, or the header where your client can send one), call `SubscribeBoard(boardId)`, and handle
        `BoardChange(boardId, pageId)`: it says the board changed, so load what you need again.
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
