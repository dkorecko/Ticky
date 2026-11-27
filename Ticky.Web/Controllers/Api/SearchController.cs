using System.ComponentModel;
using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>Find cards across all your boards.</summary>
[Route(Constants.Api.RoutePrefix + "/search")]
[Tags("Search")]
public class SearchController(IDbContextFactory<DataContext> dbContextFactory) : ApiControllerBase
{
    private const int MAX_LIMIT = 100;

    /// <summary>Search cards</summary>
    /// <remarks>
    /// Cards on every board you can open whose name or key contains the text, like the search box. A card whose key
    /// is exactly the text (e.g. <c>TKY-12</c>) comes first, then the newest. To filter one board's cards, list them
    /// with filters instead.
    /// </remarks>
    /// <param name="q">The text to look for, e.g. part of a name or a key like <c>TKY-12</c>.</param>
    /// <param name="limit">How many cards to return, at most 100.</param>
    /// <response code="200">The matching cards, best first.</response>
    /// <response code="400">The text is missing, or the limit isn't between 1 and 100.</response>
    [HttpGet]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<CardSummaryDTO>>> Search([FromQuery] string q, [FromQuery, DefaultValue(20)] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Invalid(nameof(q), "Give the text to search for.");

        if (limit is < 1 or > MAX_LIMIT)
            return Invalid(nameof(limit), $"The limit must be between 1 and {MAX_LIMIT}.");

        using var db = dbContextFactory.CreateDbContext();

        var cards = await db.Cards
            .AccessibleBy(UserId)
            .Search(q)
            .Take(limit)
            .AsSplitQuery()
            .Select(ApiMappings.CardSummaryProjection)
            .ToListAsync();

        return Ok(cards);
    }
}
