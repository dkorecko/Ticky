using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// Time spent on a card: your own timer, and records anyone on the board can add, change or delete. A card lists its
/// records; your running timers are in your work.
/// </summary>
[Route(Constants.Api.RoutePrefix + "/cards/{cardId:int}")]
[Tags("Time tracking")]
[ApiWrite]
[ProblemResponse(StatusCodes.Status404NotFound)]
public class TimeTrackingController(IDbContextFactory<DataContext> dbContextFactory, CardService cardService) : ApiControllerBase
{
    /// <summary>Start your timer</summary>
    /// <remarks>Starts your timer on the card. If it's already running, that timer is returned.</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <response code="200">Your running timer.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPut("timer")]
    public async Task<ActionResult<TimeRecordDTO>> StartTimer(int cardId)
    {
        var result = await cardService.StartTimerAsync(cardId, UserId);

        if (await FailureOrNotifyAsync(result, cardId) is { } refused)
            return refused;

        return await LoadAsync(result.Value!.Id);
    }

    /// <summary>Stop your timer</summary>
    /// <remarks>Stops your running timer on the card. A timer that ran less than a second is discarded.</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <response code="204">The timer is stopped.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="409">You have no timer running on the card. You can stop your timer on a board you have left.</response>
    [HttpDelete("timer")]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> StopTimer(int cardId)
    {
        return await NoContentOrFailureAsync(await cardService.StopTimerAsync(cardId, UserId), cardId);
    }

    /// <summary>Record time</summary>
    /// <remarks>Adds time you spent on the card, ending now, e.g. `"duration": "01:30:00"`.</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="request">How long.</param>
    /// <response code="201">The new record.</response>
    /// <response code="400">The duration isn't longer than zero.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPost("time-records")]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<TimeRecordDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TimeRecordDTO>> AddRecord(int cardId, [FromBody] TimeRecordRequest request)
    {
        var result = await cardService.AddTimeRecordAsync(cardId, UserId, request.Duration);

        if (await FailureOrNotifyAsync(result, cardId) is { } refused)
            return refused;

        return CreatedAtAction(nameof(CardsController.Get), "Cards", new { id = cardId }, await LoadAsync(result.Value!.Id));
    }

    /// <summary>Change a time record</summary>
    /// <remarks>Sets how long a finished record is; it then ends now. Anyone on the board can change any record, as in the app.</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="recordId">The record's id.</param>
    /// <param name="request">How long.</param>
    /// <response code="200">The record.</response>
    /// <response code="400">The duration isn't longer than zero.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card or record doesn't exist, or you aren't a member of the card's board or project.</response>
    /// <response code="409">The timer is still running.</response>
    [HttpPatch("time-records/{recordId:int}")]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimeRecordDTO>> UpdateRecord(int cardId, int recordId, [FromBody] TimeRecordRequest request)
    {
        var result = await cardService.EditTimeRecordAsync(cardId, UserId, recordId, request.Duration);

        if (await FailureOrNotifyAsync(result, cardId) is { } refused)
            return refused;

        return await LoadAsync(recordId);
    }

    /// <summary>Delete a time record</summary>
    /// <remarks>Anyone on the board can delete any record, a running timer included, as in the app.</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="recordId">The record's id.</param>
    /// <response code="204">The record is deleted.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card or record doesn't exist, or you aren't a member of the card's board or project.</response>
    [HttpDelete("time-records/{recordId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteRecord(int cardId, int recordId)
    {
        return await NoContentOrFailureAsync(await cardService.RemoveTimeRecordAsync(cardId, UserId, recordId), cardId);
    }

    private async Task<TimeRecordDTO> LoadAsync(int recordId)
    {
        using var db = dbContextFactory.CreateDbContext();

        return await db.TimeRecords.Where(x => x.Id == recordId).Select(ApiMappings.TimeRecordProjection).FirstAsync();
    }
}
