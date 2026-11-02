namespace Ticky.Internal.Helpers;

public static class UploadHelper
{
    private const string FALLBACK_EXTENSION = ".bin";

    /// <summary>
    /// Returns the extension to store an upload under, including the dot. Only plain ASCII letters and digits are
    /// kept (e.g. ".pdf"); anything else, or no extension at all, becomes ".bin".
    /// </summary>
    public static string GetSafeExtension(string originalName)
    {
        var extension = Path.GetExtension(originalName);

        if (extension.Length <= 1 || !extension[1..].All(char.IsAsciiLetterOrDigit))
            return FALLBACK_EXTENSION;

        return extension;
    }

    /// <summary>Picks a random file name that is not used yet in the folder, with a safe version of the original extension.</summary>
    public static string CreateStoredFileName(string folderPath, string originalName)
    {
        var extension = GetSafeExtension(originalName);
        string fileName;

        do
        {
            fileName = Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + extension;
        } while (File.Exists(Path.Combine(folderPath, fileName)));

        return fileName;
    }
}
