using Microsoft.AspNetCore.Authorization;

namespace Ticky.Web.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class AttachmentsController : ControllerBase
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;
    private readonly ILogger<AttachmentsController> _logger;

    public AttachmentsController(
        IDbContextFactory<DataContext> dbContextFactory,
        ILogger<AttachmentsController> logger
    )
    {
        _dbContextFactory = dbContextFactory;
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
                x.FileName == WebUtility.UrlDecode(decodedFileName)
            );

            if (attachment is null)
                return NotFound();

            var absolutePath = Path.GetFullPath(
                Path.Combine(Constants.SAVE_UPLOADED_FILES_PATH, attachment.FileName)
            );

            var contentType = "application/octet-stream";
            return PhysicalFile(absolutePath, contentType, attachment.OriginalName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while downloading attachment {FileName}", decodedFileName);
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

        var folderPath = Constants.SAVE_UPLOADED_FILES_PATH;

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        try
        {
            var extension = Path.GetExtension(file.FileName);

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = file.FileName.Contains('.')
                    ? file.FileName[(file.FileName.LastIndexOf('.') + 1)..]
                    : "bin";
                if (!extension.StartsWith('.'))
                    extension = "." + extension;
            }

            string trustedFileName;
            string path;
            do
            {
                trustedFileName = Path.GetRandomFileName();
                trustedFileName = trustedFileName[..trustedFileName.LastIndexOf('.')] + extension;
                path = Path.Combine(folderPath, trustedFileName);
            } while (System.IO.File.Exists(path));

            await using (var fs = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            using var db = _dbContextFactory.CreateDbContext();

            var card = await db
                .Cards.Include(x => x.Activities)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == cardId);

            if (card is null)
                return NotFound();

            var attachment = new Attachment
            {
                CardId = cardId,
                FileName = trustedFileName,
                OriginalName = file.FileName
            };

            card.Attachments.Add(attachment);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            int userId = 0;

            if (!string.IsNullOrWhiteSpace(userIdClaim))
                int.TryParse(userIdClaim, out userId);

            if (userId == 0)
                return Unauthorized();

            var safeFileName = WebUtility.HtmlEncode(file.FileName);

            card.Activities.Add(
                new Activity
                {
                    Text = $"<b>uploaded</b> file named <b>{safeFileName}</b>",
                    UserId = userId,
                    CardId = cardId
                }
            );

            await db.SaveChangesAsync();

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
