namespace Ticky.Internal.Services
{
    public class AvatarService
    {
        private readonly HttpClient _httpClient;

        public AvatarService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

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

            var fileName = UploadHelper.CreateStoredFileName(folderPath, "avatar.png");
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
