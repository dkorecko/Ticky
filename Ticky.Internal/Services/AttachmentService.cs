namespace Ticky.Internal.Services;

/// <summary>
/// Card attachments as files: saving uploads (from the card dialog, pasting and the API) and finding a stored file for
/// someone allowed to download it. The attachment records themselves are kept by <see cref="CardService"/>.
/// </summary>
public class AttachmentService(CardService cardService, IDbContextFactory<DataContext> dbContextFactory, ILogger<AttachmentService> logger)
{
    /// <summary>A stored attachment and where its file is.</summary>
    public record StoredFile(Attachment Attachment, string Path);

    /// <summary>
    /// Saves an uploaded file under a new random name and attaches it to the card. Files larger than
    /// <see cref="Constants.Limits.MAX_FILE_SIZE"/> are refused.
    /// </summary>
    /// <param name="length">The file's size in bytes, as the upload states it.</param>
    public async Task<OperationResult<Attachment>> AddAsync(int cardId, int userId, Stream content, string originalName, long length)
    {
        originalName = Path.GetFileName(originalName ?? string.Empty);

        if (originalName.Length == 0)
            return OperationResult.Invalid("File", "The file needs a name.");

        if (length > Constants.Limits.MAX_FILE_SIZE)
            return OperationResult.Invalid("File", $"The file {originalName} is larger than {Constants.Limits.MAX_FILE_SIZE / 1024 / 1024} MB.");

        // Checked before writing, so no file is stored for a card the user can't open.
        if (!await cardService.CanAccessCardAsync(cardId, userId))
            return OperationResult.NotFound(CardService.CARD_NOT_FOUND);

        var folderPath = Constants.SAVE_UPLOADED_FILES_PATH;
        Directory.CreateDirectory(folderPath);

        var storedName = UploadHelper.CreateStoredFileName(folderPath, originalName);
        var path = Path.Combine(folderPath, storedName);

        try
        {
            await using (var file = new FileStream(path, FileMode.CreateNew))
                await content.CopyToAsync(file);

            var result = await cardService.AddAttachmentAsync(cardId, userId, storedName, originalName);

            // The card was deleted meanwhile: drop the orphaned file.
            if (!result.Succeeded)
                File.Delete(path);

            return result;
        }
        catch
        {
            if (File.Exists(path))
                File.Delete(path);

            throw;
        }
    }

    /// <summary>The attachment stored under this file name, if the user can open its card and the file exists.</summary>
    public Task<StoredFile?> FindByFileNameAsync(string fileName, int userId) =>
        FindAsync(x => x.FileName == fileName, userId);

    /// <summary>The card's attachment, if the user can open the card and the file exists.</summary>
    public Task<StoredFile?> FindAsync(int cardId, int attachmentId, int userId) =>
        FindAsync(x => x.Id == attachmentId && x.CardId == cardId, userId);

    private async Task<StoredFile?> FindAsync(System.Linq.Expressions.Expression<Func<Attachment, bool>> which, int userId)
    {
        using var db = dbContextFactory.CreateDbContext();
        var attachment = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(which);

        // The same answer as a missing file for someone who can't see the card, so links don't reveal attachments.
        if (attachment is null || !await db.Cards.AccessibleBy(userId).AnyAsync(x => x.Id == attachment.CardId))
            return null;

        var path = Path.GetFullPath(Path.Combine(Constants.SAVE_UPLOADED_FILES_PATH, attachment.FileName));

        if (!File.Exists(path))
        {
            logger.LogWarning("The file of attachment {AttachmentId} is missing.", attachment.Id);
            return null;
        }

        return new StoredFile(attachment, path);
    }
}
