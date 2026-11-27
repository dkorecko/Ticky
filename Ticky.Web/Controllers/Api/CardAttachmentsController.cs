using System.Net.Mime;
using Ticky.Base.DTOs.Api;

namespace Ticky.Web.Controllers.Api;

/// <summary>Files attached to a card. A card lists its attachments, each with a download link.</summary>
[Route(Constants.Api.RoutePrefix + "/cards/{cardId:int}/attachments")]
[Tags("Attachments")]
[ProblemResponse(StatusCodes.Status404NotFound)]
public class CardAttachmentsController(AttachmentService attachmentService, CardService cardService) : ApiControllerBase
{
    /// <summary>Upload an attachment</summary>
    /// <remarks>
    /// Attaches a file to the card, sent as `multipart/form-data` in a field named `file`, up to 15 MB. For example:
    /// `curl -F "file=@report.pdf" …/cards/12/attachments`. Only with a read and write token: a signed-in browser
    /// uploads through the app itself.
    /// </remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="file">The file.</param>
    /// <response code="201">The new attachment.</response>
    /// <response code="400">No file was sent, or it's larger than 15 MB.</response>
    /// <response code="403">The token is read-only, or the request has no token.</response>
    /// <response code="404">The card doesn't exist, or you aren't a member of its board or project.</response>
    [HttpPost]
    [ApiWrite(tokenOnly: true)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(Constants.Limits.MAX_FILE_SIZE + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = Constants.Limits.MAX_FILE_SIZE + 64 * 1024)]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<AttachmentDTO>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AttachmentDTO>> Upload(int cardId, IFormFile? file)
    {
        if (file is null)
            return Invalid(nameof(file), "Send the file in a form field named file.");

        await using var content = file.OpenReadStream();
        var result = await attachmentService.AddAsync(cardId, UserId, content, file.FileName, file.Length);

        if (await FailureOrNotifyAsync(result, cardId) is { } refused)
            return refused;

        return CreatedAtAction(nameof(Download), new { cardId, attachmentId = result.Value!.Id }, result.Value.ToDto());
    }

    /// <summary>Download an attachment</summary>
    /// <remarks>The file as uploaded, always as a download (`Content-Disposition: attachment`).</remarks>
    /// <param name="cardId">The card's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <response code="200">The file.</response>
    /// <response code="404">The card or attachment doesn't exist, or you aren't a member of the card's board or project.</response>
    [HttpGet("{attachmentId:int}/content")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, MediaTypeNames.Application.Octet)]
    public async Task<ActionResult> Download(int cardId, int attachmentId)
    {
        var stored = await attachmentService.FindAsync(cardId, attachmentId, UserId);

        if (stored is null)
            return NotFound();

        // Served as bytes, never as a page: an uploaded .html file must not run on this domain.
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(stored.Path, MediaTypeNames.Application.Octet, stored.Attachment.OriginalName);
    }

    /// <summary>Delete an attachment</summary>
    /// <param name="cardId">The card's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <response code="204">The attachment is deleted.</response>
    /// <response code="403">The token is read-only.</response>
    /// <response code="404">The card or attachment doesn't exist, or you aren't a member of the card's board or project.</response>
    [HttpDelete("{attachmentId:int}")]
    [ApiWrite]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(int cardId, int attachmentId)
    {
        return await NoContentOrFailureAsync(await cardService.RemoveAttachmentAsync(cardId, UserId, attachmentId), cardId);
    }
}
