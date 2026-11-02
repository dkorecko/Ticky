using Microsoft.AspNetCore.Authorization;

namespace Ticky.Web.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class AttachmentsController : ControllerBase
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;
    private readonly CardService _cardService;
    private readonly ILogger<AttachmentsController> _logger;

    public AttachmentsController(
        IDbContextFactory<DataContext> dbContextFactory,
        CardService cardService,
        ILogger<AttachmentsController> logger
    )
    {
        _dbContextFactory = dbContextFactory;
        _cardService = cardService;
        _logger = logger;
    }

    [HttpGet("download/{*fileName}")]
    public async Task<IActionResult> Download(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest();

        var decodedFileName = WebUtility.UrlDecode(fileName);
        string[] forbiddenChars = ["..", "/", "\\", "\n", "\r"];

        if (forbiddenChars.Any(decodedFileName.Contains))
            return BadRequest();

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var attachment = await db.Attachments.FirstOrDefaultAsync(x =>
                x.FileName == decodedFileName
            );

            if (attachment is null)
                return NotFound();

            var absolutePath = Path.GetFullPath(
                Path.Combine(Constants.SAVE_UPLOADED_FILES_PATH, attachment.FileName)
            );

            if (!System.IO.File.Exists(absolutePath))
                return NotFound();

            var contentType = "application/octet-stream";
            Response.Headers.XContentTypeOptions = "nosniff";
            return PhysicalFile(absolutePath, contentType, attachment.OriginalName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while downloading an attachment");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("preview/{*fileName}")]
    public async Task<IActionResult> Preview(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest();

        var decodedFileName = WebUtility.UrlDecode(fileName);
        string[] forbiddenChars = ["..", "/", "\\", "\n", "\r"];

        if (forbiddenChars.Any(decodedFileName.Contains))
            return BadRequest();

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var attachment = await db.Attachments.FirstOrDefaultAsync(x =>
                x.FileName == decodedFileName
            );

            if (attachment is null || !AttachmentHelper.IsImage(attachment))
                return NotFound();

            var absolutePath = Path.GetFullPath(
                Path.Combine(Constants.SAVE_UPLOADED_FILES_PATH, attachment.FileName)
            );

            if (!System.IO.File.Exists(absolutePath))
                return NotFound();

            var contentTypeProvider =
                new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            contentTypeProvider.Mappings[Constants.Attachments.AVIF_FILE_EXTENSION] =
                Constants.Attachments.AVIF_CONTENT_TYPE;

            if (!contentTypeProvider.TryGetContentType(attachment.OriginalName, out var contentType))
                return NotFound();

            Response.Headers.XContentTypeOptions = "nosniff";
            return PhysicalFile(absolutePath, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while previewing an attachment");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] int cardId)
    {
        if (User is null)
            return Unauthorized();

        if (file is null)
            return BadRequest("No file");

        if (file.Length > Constants.Limits.MAX_FILE_SIZE)
            return BadRequest("File too large");

        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) || userId == 0)
            return Unauthorized();

        // Same response as a missing card, so the endpoint doesn't reveal which card ids exist.
        if (!await _cardService.CanAccessCardAsync(cardId, userId))
            return NotFound();

        var folderPath = Constants.SAVE_UPLOADED_FILES_PATH;

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        try
        {
            var trustedFileName = UploadHelper.CreateStoredFileName(folderPath, file.FileName);
            var path = Path.Combine(folderPath, trustedFileName);

            await using (var fs = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            if (!await _cardService.AddAttachmentAsync(cardId, userId, trustedFileName, file.FileName))
            {
                System.IO.File.Delete(path);
                return NotFound();
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while uploading an attachment for card {CardId}",
                cardId
            );

            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
