using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// Links between cards, like "is blocked by". Each link has an opposite on the other card, added and removed with it.
/// A card lists its links.
/// </summary>
[Route(Constants.Api.RoutePrefix + "/cards/{cardId:int}/links")]
[Tags("Links")]
[ApiWrite]
[ProblemResponse(StatusCodes.Status404NotFound)]
public class LinksController(IDbContextFactory<DataContext> dbContextFactory, CardService cardService) : ApiControllerBase
{
    /// <summary>Link two cards</summary>
    /// <remarks>
    /// Links the card to another card you can open, possibly on another board. The other card gets the opposite link,
    /// e.g. "blocks" for "is blocked by". Both cards' activity records it.
    /// </remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="request">The other card and the relationship.</param>
    /// <response code="201">The new link, as seen from this card.</response>
    /// <response code="400">The relationship is unknown, or the target card is this card or one you can't open.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    /// <response code="409">The cards are already linked.</response>
    [HttpPost]
    [ProblemResponse(StatusCodes.Status400BadRequest), ProblemResponse(StatusCodes.Status409Conflict)]
    [ProducesResponseType<CardLinkDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CardLinkDTO>> Create(int cardId, [FromBody] LinkCardRequest request)
    {
        var result = await cardService.LinkCardsAsync(cardId, UserId, request.TargetCardId, request.Category);

        if (await FailureOrNotifyAsync(result, cardId, request.TargetCardId) is { } refused)
            return refused;

        using var db = dbContextFactory.CreateDbContext();
        var link = await db.CardLinks
            .Where(x => x.Id == result.Value!.Id)
            .Select(ApiMappings.CardLinkProjection)
            .FirstAsync();

        return CreatedAtAction(nameof(CardsController.Get), "Cards", new { id = cardId }, link);
    }

    /// <summary>Remove a link</summary>
    /// <remarks>Removes the link and its opposite on the other card.</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="linkId">The link's id, from the card's links.</param>
    /// <response code="204">The cards are no longer linked.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card or link doesn't exist, or you aren't a member of the card's board or project.</response>
    [HttpDelete("{linkId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(int cardId, int linkId)
    {
        using var db = dbContextFactory.CreateDbContext();
        var otherCardId = await db.CardLinks.Where(x => x.Id == linkId && x.CardOneId == cardId).Select(x => x.CardTwoId).FirstOrDefaultAsync();
        return await NoContentOrFailureAsync(await cardService.RemoveLinkAsync(cardId, UserId, linkId), cardId, otherCardId);
    }
}
