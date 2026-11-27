using Microsoft.AspNetCore.Authorization;
using Ticky.Web.Helpers;

namespace Ticky.Web.Controllers;

/// <summary>
/// Attachment files for the web UI: downloads, image previews, and uploads pasted into the card dialog. Every request
/// checks that the signed-in user can open the attachment's card, so a link stops working for someone removed from
/// the board. The public API has its own endpoints (see <c>CardAttachmentsController</c>).
/// </summary>
[Authorize]
[Route("api/[controller]")]
[ApiController]
public class AttachmentsController : ControllerBase
{
    private static readonly string[] ForbiddenFileNameParts = ["..", "/", "\\", "\n", "\r"];

    private readonly AttachmentService _attachmentService;
    private readonly ILogger<AttachmentsController> _logger;

    public AttachmentsController(AttachmentService attachmentService, ILogger<AttachmentsController> logger)
    {
        _attachmentService = attachmentService;
        _logger = logger;
    }

    [HttpGet("download/{*fileName}")]
    public async Task<IActionResult> Download(string fileName)
    {
        var stored = await FindAsync(fileName);

        if (stored is null)
            return NotFound();

        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(stored.Path, "application/octet-stream", stored.Attachment.OriginalName);
    }

    [HttpGet("preview/{*fileName}")]
    public async Task<IActionResult> Preview(string fileName)
    {
        var stored = await FindAsync(fileName);

        if (stored is null || !AttachmentContentTypes.TryGetImageContentType(stored.Attachment, out var contentType))
            return NotFound();

        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(stored.Path, contentType, enableRangeProcessing: true);
    }

    // A signed-in cookie plus a multipart body is a request any page on a sibling origin can send, so the upload
    // needs the antiforgery token the app hands to the paste handler (sent in the header named in Program.cs).
    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] int cardId)
    {
        if (file is null)
            return BadRequest("No file");

        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) || userId == 0)
            return Unauthorized();

        try
        {
            await using var content = file.OpenReadStream();
            var result = await _attachmentService.AddAsync(cardId, userId, content, file.FileName, file.Length);

            // A missing card and one the user can't open look the same, so card ids don't leak.
            return result.Status switch
            {
                OperationStatus.Succeeded => Ok(new { success = true }),
                OperationStatus.NotFound => NotFound(),
                _ => BadRequest(result.Message),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while uploading an attachment for card {CardId}", cardId);

            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>The attachment stored under the name in the URL, if the signed-in user can open its card.</summary>
    private async Task<AttachmentService.StoredFile?> FindAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var decodedFileName = WebUtility.UrlDecode(fileName);

        if (ForbiddenFileNameParts.Any(decodedFileName.Contains))
            return null;

        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return null;

        return await _attachmentService.FindByFileNameAsync(decodedFileName, userId);
    }
}
