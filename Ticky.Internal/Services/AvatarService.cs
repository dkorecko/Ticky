namespace Ticky.Internal.Services
{
    public class AvatarService
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Starts the file name of every generated avatar, which tells it apart from an uploaded picture (those get a
        /// plain random name). That way the avatar can follow a name change without replacing a picture the user chose.
        /// Avatars generated before this prefix was added look like uploads, so they are left alone.
        /// </summary>
        public const string GENERATED_FILE_PREFIX = "generated-";

        public AvatarService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>Whether the file is an avatar this service generated, rather than one the user uploaded.</summary>
        public static bool IsGenerated(string? fileName) =>
            fileName?.StartsWith(GENERATED_FILE_PREFIX, StringComparison.Ordinal) == true;

        /// <summary>
        /// Whether generated avatars show the user's name (their initials). In fully offline mode every user gets the
        /// same placeholder instead, so there is nothing to update when a name changes.
        /// </summary>
        public static bool ShowsName => !Constants.FULLY_OFFLINE;

        /// <summary>
        /// Generates a default avatar showing the user's initials and returns its file name.
        /// The name is only sent to the avatar generator; the file itself gets a random name, since display names
        /// are chosen freely at sign-up and may contain characters like "../", "#" or "?" (unused files are
        /// removed by CleanupHostedService).
        /// </summary>
        public async Task<string> FetchAvatarAsync(string name)
        {
            var folderPath = Constants.SAVE_UPLOADED_IMAGES_PATH;
            Directory.CreateDirectory(folderPath);

            var fileName = UploadHelper.CreateStoredFileName(folderPath, "avatar.png", GENERATED_FILE_PREFIX);
            var targetFilePath = Path.Combine(folderPath, fileName);

            if (!Constants.FULLY_OFFLINE)
            {
                await using var stream = await _httpClient.GetStreamAsync(
                    $"https://ui-avatars.com/api/?background=random&name={Uri.EscapeDataString(name)}"
                );
                await using var fs = File.Create(targetFilePath);
                await stream.CopyToAsync(fs);
            }
            else
            {
                File.Copy(
                    Path.Combine(Constants.WWW_ROOT, "images", "question_mark_avatar.png"),
                    targetFilePath
                );
            }

            return fileName;
        }
    }
}
