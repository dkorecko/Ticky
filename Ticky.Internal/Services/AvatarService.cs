namespace Ticky.Internal.Services
{
    public class AvatarService
    {
        private readonly HttpClient _httpClient;

        public AvatarService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> FetchAvatarAsync(string name)
        {
            if (!Directory.Exists(Constants.SAVE_UPLOADED_PATH))
                Directory.CreateDirectory(Constants.SAVE_UPLOADED_PATH);

            if (!Directory.Exists(Constants.SAVE_UPLOADED_IMAGES_PATH))
                Directory.CreateDirectory(Constants.SAVE_UPLOADED_IMAGES_PATH);

            var fileName = name + ".png";
            var targetFilePath = $"{Constants.SAVE_UPLOADED_IMAGES_PATH}/{fileName}";

            if (File.Exists(targetFilePath))
                return fileName;

            if (!Constants.FULLY_OFFLINE)
            {
                var stream = await _httpClient.GetStreamAsync(
                    $"https://ui-avatars.com/api/?background=random&name={Uri.EscapeDataString(name)}"
                );
                using var fs = File.Create(targetFilePath);
                stream.CopyTo(fs);
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
