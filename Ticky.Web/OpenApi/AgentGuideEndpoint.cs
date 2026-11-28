using System.Net.Mime;
using System.Text;

namespace Ticky.Web.OpenApi;

/// <summary>
/// Serves the guide for AI agents (<c>Docs/AgentGuide.md</c>) at <see cref="Constants.Api.AgentGuidePath"/>.
/// </summary>
public static class AgentGuideEndpoint
{
    /// <summary>Where the guide is, under the content root. The project copies it to the build and publish output.</summary>
    public static readonly string[] GuideFile = ["Docs", "AgentGuide.md"];

    /// <summary>
    /// Maps the guide, read once now: it never changes while the app runs. Not static files, since only this one
    /// file is public and it needs a content type browsers show as text rather than download.
    /// </summary>
    public static IEndpointConventionBuilder MapAgentGuide(this IEndpointRouteBuilder endpoints)
    {
        var environment = endpoints.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var guide = Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine([environment.ContentRootPath, .. GuideFile])));

        return endpoints
            .MapGet(
                Constants.Api.AgentGuidePath,
                () => Results.Bytes(guide, $"{MediaTypeNames.Text.Markdown}; charset=utf-8")
            )
            .ExcludeFromDescription();
    }
}
