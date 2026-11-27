using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>Who you are, and what you're working on.</summary>
[Route(Constants.Api.RoutePrefix + "/me")]
[Tags("Me")]
public class MeController(IDbContextFactory<DataContext> dbContextFactory) : ApiControllerBase
{
    /// <summary>Get yourself</summary>
    /// <remarks>The user the API acts as, and what the token may do. Use the id to assign yourself to cards.</remarks>
    /// <response code="200">You.</response>
    [HttpGet]
    public async Task<ActionResult<MeDTO>> Get()
    {
        using var db = dbContextFactory.CreateDbContext();
        var user = await db.Users.AsNoTracking().FirstAsync(x => x.Id == UserId);

        // Only token callers carry a scope; a signed-in browser may do everything.
        var scope = Enum.TryParse<ApiTokenScope>(User.FindFirstValue(Constants.Api.ScopeClaim), out var tokenScope)
            ? tokenScope
            : ApiTokenScope.ReadWrite;

        return new MeDTO(user.Id, user.DisplayName, user.Email, scope);
    }

    /// <summary>Get your work</summary>
    /// <remarks>
    /// What the home page's Your work shows: open cards assigned to you (not in a finished column, not snoozed),
    /// soonest deadline first, and your running timers.
    /// </remarks>
    /// <response code="200">Your cards and timers.</response>
    [HttpGet("work")]
    public async Task<ActionResult<WorkDTO>> Work()
    {
        using var db = dbContextFactory.CreateDbContext();

        // Split: assignee and label ids are two collections per card, which one joined query would multiply.
        var cards = await db.Cards
            .AccessibleBy(UserId)
            .WorkOf(UserId)
            .AsSplitQuery()
            .Select(ApiMappings.CardSummaryProjection)
            .ToListAsync();

        var timers = await db.TimeRecords
            .RunningOf(UserId)
            .Select(ApiMappings.TimeRecordProjection)
            .ToListAsync();

        return new WorkDTO(cards, timers);
    }
}
