using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// A card's reminders: when one is due, the card's assignees and creator get an e-mail, if the instance sends e-mail. A
/// card lists its reminders.
/// </summary>
[Route(Constants.Api.RoutePrefix + "/cards/{cardId:int}/reminders")]
[Tags("Reminders")]
[ApiWrite]
[ProblemResponse(StatusCodes.Status404NotFound)]
public class RemindersController(CardService cardService) : ApiControllerBase
{
    /// <summary>Add a reminder</summary>
    /// <param name="cardId">The card's id.</param>
    /// <param name="request">When.</param>
    /// <response code="201">The new reminder.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPost]
    [ProducesResponseType<ReminderDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReminderDTO>> Create(int cardId, [FromBody] AddReminderRequest request)
    {
        var result = await cardService.AddReminderAsync(cardId, UserId, request.At);

        if (await FailureOrNotifyAsync(result, cardId) is { } refused)
            return refused;

        return CreatedAtAction(nameof(CardsController.Get), "Cards", new { id = cardId }, result.Value!.ToDto());
    }

    /// <summary>Delete a reminder</summary>
    /// <param name="cardId">The card's id.</param>
    /// <param name="reminderId">The reminder's id.</param>
    /// <response code="204">The reminder is deleted.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card or reminder doesn't exist, or you aren't a member of the card's board or project.</response>
    [HttpDelete("{reminderId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(int cardId, int reminderId)
    {
        return await NoContentOrFailureAsync(await cardService.RemoveReminderAsync(cardId, UserId, reminderId), cardId);
    }
}
