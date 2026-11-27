using System.Net.Mime;
using Microsoft.AspNetCore.RateLimiting;
using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// Base of the public API's controllers: API conventions (ProblemDetails errors, model validation), access for
/// signed-in users and API tokens, the caller's user id, and telling open boards about changes.
/// </summary>
/// <remarks>
/// The rules live in the services the web UI uses too (<see cref="CardService"/>, <see cref="BoardService"/>, …), so
/// the API allows exactly what the UI does. Controllers map requests to them and their results to responses.
/// </remarks>
[ApiController]
[Authorize(Policy = Constants.Policies.ApiCallerAuthenticated)]
[EnableRateLimiting(Constants.Api.RateLimitPolicy)]
[ProducesJson]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, MediaTypeNames.Application.ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, MediaTypeNames.Application.ProblemJson)]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The most activity entries one page holds.</summary>
    public const int MAX_ACTIVITY_PAGE = 200;

    /// <summary>
    /// The caller's user id. The authorization policy only lets signed-in users and valid tokens through, and both
    /// carry it.
    /// </summary>
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// The response for a refused change: 404 for something missing or out of reach, 403 for a change the caller may
    /// not make, 409 for a clash with the current state, and 400 with the field for an invalid value.
    /// </summary>
    protected ActionResult Failure(IOperationResult result)
    {
        if (result.Status == OperationStatus.Invalid)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message ?? "The value isn't valid.");
            return ValidationProblem(ModelState);
        }

        var status = result.Status switch
        {
            OperationStatus.NotFound => StatusCodes.Status404NotFound,
            OperationStatus.Forbidden => StatusCodes.Status403Forbidden,
            OperationStatus.Conflict => StatusCodes.Status409Conflict,
            _ => throw new InvalidOperationException($"{result.Status} isn't a failure."),
        };

        return Problem(detail: result.Message, statusCode: status);
    }

    /// <summary>A 400 for one invalid field of the request, like model validation gives.</summary>
    protected ActionResult Invalid(string field, string message) =>
        Failure(OperationResult.Invalid(field, message));

    /// <summary>
    /// Makes open boards reload, like the browser does after its own changes. Guid.Empty is no page's id, so every
    /// viewer reloads.
    /// </summary>
    protected async Task NotifyBoardsAsync(params IEnumerable<int> boardIds)
    {
        var hub = HttpContext.RequestServices.GetRequiredService<IHubContext<UpdateHub>>();

        foreach (var boardId in boardIds.Distinct())
            await hub.Clients.Group(UpdateHub.BoardGroup(boardId)).SendAsync(nameof(UpdateHub.BoardChange), boardId, Guid.Empty);
    }

    /// <summary>
    /// For a change of cards: the failure response if it was refused; otherwise null, after telling the cards' boards
    /// about it, so the action goes on to its own response.
    /// </summary>
    protected async Task<ActionResult?> FailureOrNotifyAsync(IOperationResult result, params IEnumerable<int> cardIds)
    {
        if (!result.Succeeded)
            return Failure(result);

        await NotifyCardBoardsAsync(cardIds);
        return null;
    }

    /// <summary>Like <see cref="FailureOrNotifyAsync"/>, for a change that answers 204 when it went through.</summary>
    protected async Task<ActionResult> NoContentOrFailureAsync(IOperationResult result, params IEnumerable<int> cardIds) =>
        await FailureOrNotifyAsync(result, cardIds) ?? NoContent();

    /// <summary>Tells the boards of the cards that they changed.</summary>
    protected async Task NotifyCardBoardsAsync(params IEnumerable<int> cardIds)
    {
        var ids = cardIds.ToList();
        var dbContextFactory = HttpContext.RequestServices.GetRequiredService<IDbContextFactory<DataContext>>();
        using var db = dbContextFactory.CreateDbContext();

        var boardIds = await db.Cards.Where(x => ids.Contains(x.Id)).Select(x => x.Column.BoardId).ToListAsync();
        await NotifyBoardsAsync(boardIds);
    }

    /// <summary>
    /// A page of the activity, newest first, after the <paramref name="before"/> entry if given. Shared by boards and
    /// cards.
    /// </summary>
    protected async Task<ActionResult<ActivityPageDTO>> ActivityPageAsync(IQueryable<Activity> activities, int? before, int limit, ActivityType? type)
    {
        if (limit is < 1 or > MAX_ACTIVITY_PAGE)
            return Invalid(nameof(limit), $"The limit must be between 1 and {MAX_ACTIVITY_PAGE}.");

        if (type is not null)
            activities = activities.Where(x => x.ActivityType == type);

        if (before is not null)
        {
            var cursor = await activities.Where(x => x.Id == before).Select(x => new { x.CreatedAt, x.Id }).FirstOrDefaultAsync();

            if (cursor is null)
                return Invalid(nameof(before), "There is no such activity here.");

            activities = activities.Where(x => x.CreatedAt < cursor.CreatedAt || (x.CreatedAt == cursor.CreatedAt && x.Id < cursor.Id));
        }

        // One extra entry tells whether there is an older page.
        var entries = await activities
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Card)
                .ThenInclude(x => x.Column)
                    .ThenInclude(x => x.Board)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit + 1)
            .ToListAsync();

        var page = entries.Take(limit).Select(x => x.ToDto()).ToList();
        return new ActivityPageDTO(page, entries.Count > limit ? page[^1].Id : null);
    }
}
