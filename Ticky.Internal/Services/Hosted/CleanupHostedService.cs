namespace Ticky.Internal.Services.Hosted;

public class CleanupHostedService : AbstractHostedService<CleanupHostedService>
{
    public CleanupHostedService(IServiceScopeFactory serviceScopeFactory)
        : base(serviceScopeFactory, TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)) { }

    protected override async void OnRun()
    {
        await CleanCodes();
        await DeleteUnusedProfilePictures();
        await DeleteUnlinkedAttachments();
    }

    private async Task CleanCodes()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>()!;

        var codesForDeletion = await db
            .Codes.Include(x => x.User)
            .Where(x =>
                DateTime.Now.AddHours(-1) > x.CreatedAt || (x.User != null && x.User.EmailConfirmed)
            )
            .ToListAsync();
        int deletedAccounts = 0;

        foreach (var code in codesForDeletion)
        {
            if (code.User is not null && !code.User.EmailConfirmed)
            {
                db.Users.Remove(code.User);
                deletedAccounts++;
            }

            db.Codes.Remove(code);
        }

        if (codesForDeletion.Any())
        {
            await db.SaveChangesAsync();
            Logger.LogInformation(
                $"{codesForDeletion.Count} codes have been deleted alongside {deletedAccounts} unconfirmed accounts."
            );
        }
    }

    private async Task DeleteUnusedProfilePictures()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>()!;
        var allProfilePictureNames = await db
            .Users.Select(x => x.ProfilePictureFileName)
            .Where(x => x != null)
            .ToListAsync();

        var folderPath = Constants.SAVE_UPLOADED_IMAGES_PATH;

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        var unlinkedUploadedImages = Directory
            .GetFiles(folderPath)
            .Where(x =>
                !allProfilePictureNames.Contains(
                    x[(Math.Max(x.LastIndexOf('/'), x.LastIndexOf('\\')) + 1)..]
                )
            )
            .ToList();

        foreach (var unlinkedUploadedImage in unlinkedUploadedImages)
        {
            File.Delete(unlinkedUploadedImage);
        }

        if (unlinkedUploadedImages.Any())
            Logger.LogInformation(
                $"{unlinkedUploadedImages.Count} images have been deleted due to being unlinked."
            );
    }

    private async Task DeleteUnlinkedAttachments()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>()!;
        var allFileNames = await db.Attachments.Select(x => x.FileName).ToListAsync();

        var folderPath = Constants.SAVE_UPLOADED_FILES_PATH;

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        var unlinkedAttachments = Directory
            .GetFiles(folderPath)
            .Where(x =>
                !allFileNames.Contains(x[(Math.Max(x.LastIndexOf('/'), x.LastIndexOf('\\')) + 1)..])
            )
            .ToList();

        foreach (var unlinkedAttachment in unlinkedAttachments)
        {
            File.Delete(unlinkedAttachment);
        }

        if (unlinkedAttachments.Any())
            Logger.LogInformation(
                $"{unlinkedAttachments.Count} attachments have been deleted due to being unlinked."
            );
    }
}
