using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using RegexMatch = System.Text.RegularExpressions.Match;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Ticky.Web.Controllers.Api;
using Ticky.Web.OpenApi;
using BaseConstants = Ticky.Base.Constants;

namespace Ticky.Units;

/// <summary>
/// Keeps the guide for AI agents (Ticky.Web/Docs/AgentGuide.md) honest: every API path it mentions has to be a real
/// route, with the HTTP method it names, and the file has to be served.
/// </summary>
/// <remarks>
/// The guide writes paths in two forms this test understands. A <c>curl</c> command in a code block names the full
/// path (<c>$TICKY_URL/api/v1/cards/$CARD</c>) and its method (<c>-X POST</c>; none means GET). Prose names a path in
/// inline code, with or without the method and the <c>/api/v1</c> prefix: <c>GET /boards/{id}</c>. In a path, a
/// number, a <c>{placeholder}</c> or a <c>$VARIABLE</c> stands for a route parameter.
/// </remarks>
public class AgentGuideTest
{
    private static readonly string[] HttpMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    /// <summary>The first segments of API paths, which tell a path in inline code from other code like /updatehub.</summary>
    private static readonly string[] ApiAreas = ["me", "boards", "cards", "search", "projects", "columns", "labels"];

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ticky.sln")))
                directory = directory.Parent;

            return directory?.FullName ?? throw new InvalidOperationException("Ticky.sln wasn't found above the test output.");
        }
    }

    private static string GuidePath => Path.Combine([RepositoryRoot, "Ticky.Web", .. AgentGuideEndpoint.GuideFile]);

    [Test]
    public void EveryPathInTheGuideIsARealRoute()
    {
        var routes = ApiRoutes();
        var mentions = PathsIn(File.ReadAllText(GuidePath));

        // A parser that silently finds nothing would pass forever.
        Assert.That(mentions.Count, Is.GreaterThan(20), "The guide's API paths weren't found.");

        var unknown = mentions
            .Where(mention => !routes.Any(route => route.Matches(mention)))
            .Select(mention => mention.ToString())
            .Distinct()
            .ToList();

        Assert.That(unknown, Is.Empty, "The guide mentions API paths that don't exist:\n" + string.Join("\n", unknown));
    }

    [Test]
    public void AMadeUpPathIsNotARoute()
    {
        var routes = ApiRoutes();

        var mentions = PathsIn(
            """
            `GET /boards/{id}`
            `PATCH /cards/{id}/nonsense`

            ```bash
            curl -s -X DELETE "$TICKY_URL/api/v1/boards/$BOARD/cards" -H "Authorization: Bearer $TICKY_TOKEN"
            curl -s "$TICKY_URL/api/v1/cards/$CARD/move" -H "Authorization: Bearer $TICKY_TOKEN"
            ```
            """
        );

        Assert.That(mentions, Has.Count.EqualTo(4));
        Assert.That(
            mentions.Where(mention => !routes.Any(route => route.Matches(mention))).Select(x => x.ToString()),
            Is.EquivalentTo(new[] { "PATCH /cards/{}/nonsense", "DELETE /boards/{}/cards", "GET /cards/{}/move" })
        );
    }

    [Test]
    public async Task TheGuideIsServedAsMarkdown()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.Combine(RepositoryRoot, "Ticky.Web"),
        });

        // A real server on a free port: this test project has no in-memory test server.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        await using var app = builder.Build();
        app.MapAgentGuide();
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            var response = await client.GetAsync(BaseConstants.Api.AgentGuidePath);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.ToString(), Is.EqualTo("text/markdown; charset=utf-8"));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(File.ReadAllText(GuidePath)));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    /// <summary>A path the guide mentions: segments lower-cased, with every parameter as <c>{}</c>.</summary>
    private sealed record Mention(string? Method, string Path)
    {
        public override string ToString() => $"{Method ?? "(any method)"} {Path}";
    }

    private sealed record Route(string Method, string Path)
    {
        public bool Matches(Mention mention) => Path == mention.Path && (mention.Method is null || mention.Method == Method);
    }

    private static List<Mention> PathsIn(string guide)
    {
        var mentions = new List<Mention>();

        // Commands in code blocks: the whole command may continue over lines with a trailing backslash.
        var blocks = Regex.Matches(guide, "```[^\n]*\n(.*?)```", RegexOptions.Singleline);

        foreach (var block in blocks.Cast<RegexMatch>())
        {
            foreach (var command in block.Groups[1].Value.Replace("\\\n", " ").Split('\n'))
            {
                if (!command.TrimStart().StartsWith("curl "))
                    continue;

                var path = Regex.Match(command, @"\$TICKY_URL/api/v1(/[^\s""'?]*)");

                if (!path.Success)
                    continue;

                var method = Regex.Match(command, @"-X\s+(\w+)");
                mentions.Add(new Mention(method.Success ? method.Groups[1].Value : "GET", Normalize(path.Groups[1].Value)));
            }
        }

        // Inline code in the text: an optional method, then the path, with or without the version prefix.
        var prose = Regex.Replace(guide, "```.*?```", string.Empty, RegexOptions.Singleline);

        foreach (var code in Regex.Matches(prose, "`([^`\n]+)`").Cast<RegexMatch>())
        {
            var span = Regex.Match(code.Groups[1].Value, @"^(?:(" + string.Join('|', HttpMethods) + @") )?(?:/api/v1)?(/[^\s?]*)$");

            if (!span.Success)
                continue;

            var path = span.Groups[2].Value;
            var area = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (area is null || !ApiAreas.Contains(area))
                continue;

            mentions.Add(new Mention(span.Groups[1].Success ? span.Groups[1].Value : null, Normalize(path)));
        }

        return mentions;
    }

    /// <summary>Lower-cases the path and turns numbers, <c>{names}</c> and <c>$VARIABLES</c> into <c>{}</c>.</summary>
    private static string Normalize(string path)
    {
        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => Regex.IsMatch(segment, @"^(\d+|\{\w+(:\w+)?\}|\$\w+|\$\{\w+\})$") ? "{}" : segment.ToLowerInvariant());

        return "/" + string.Join('/', segments);
    }

    /// <summary>
    /// Every route of the public API, from the controllers' attributes, without the version prefix. A method template
    /// starting with <c>/</c> replaces the controller's route, as in ASP.NET Core.
    /// </summary>
    private static List<Route> ApiRoutes()
    {
        var prefix = "/" + BaseConstants.Api.RoutePrefix;
        var routes = new List<Route>();

        var controllers = typeof(ApiControllerBase).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ApiControllerBase).IsAssignableFrom(type));

        foreach (var controller in controllers)
        {
            var controllerTemplate = controller.GetCustomAttributes<RouteAttribute>().Single().Template;

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var template = http.Template is null
                        ? controllerTemplate
                        : http.Template.StartsWith('/') ? http.Template : $"{controllerTemplate}/{http.Template}";

                    var path = "/" + template.TrimStart('/');
                    Assert.That(path, Does.StartWith(prefix + "/"), $"{controller.Name}.{action.Name} is outside the API prefix.");

                    var normalized = Normalize(path[prefix.Length..]);

                    foreach (var method in http.HttpMethods)
                        routes.Add(new Route(method, normalized));
                }
            }
        }

        Assert.That(routes.Count, Is.GreaterThan(50), "The API's routes weren't found.");
        return routes;
    }
}
