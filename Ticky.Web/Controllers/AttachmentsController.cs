namespace Ticky.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AttachmentsController : ControllerBase
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;
    private readonly IWebHostEnvironment _env;

    public AttachmentsController(
        IDbContextFactory<DataContext> dbContextFactory,
        IWebHostEnvironment env
    )
    {
        _dbContextFactory = dbContextFactory;
        _env = env;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] int cardId)
    {
        if (!Request.HasFormContentType)
            return BadRequest("Request must be multipart/form-data");

        if (file is null || file.Length == 0)
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
            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            int userId = 0;
            if (!string.IsNullOrWhiteSpace(userIdClaim))
                int.TryParse(userIdClaim, out userId);

            card.Activities.Add(
                new Activity
                {
                    Text = $"<b>uploaded</b> file named <b>{file.FileName}</b>",
                    UserId = userId,
                    CardId = cardId
                }
            );

            await db.SaveChangesAsync();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            // log if a logging facility is available
            return StatusCode(500, ex.Message);
        }
    }
}
