using Ticky.Internal.Helpers;

namespace Ticky.Units;

public class UploadHelperTest
{
    [TestCase("report.pdf", ".pdf")]
    [TestCase("photo.JPEG", ".JPEG")]
    [TestCase("archive.tar.gz", ".gz")]
    [TestCase("data.mp4", ".mp4")]
    [TestCase("README", ".bin")]
    [TestCase("trailing-dot.", ".bin")]
    [TestCase(".env", ".env")]
    [TestCase("weird.p-df", ".bin")]
    [TestCase("space.p df", ".bin")]
    [TestCase("query.png?x=1", ".bin")]
    [TestCase("unicode.pdfé", ".bin")]
    [TestCase("semicolon.html;.png", ".png")]
    public void GetSafeExtension(string originalName, string expected) =>
        Assert.That(UploadHelper.GetSafeExtension(originalName), Is.EqualTo(expected));

    [Test]
    public void CreateStoredFileName_IsRandomAndKeepsSafeExtension()
    {
        var folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(folder);

        try
        {
            var first = UploadHelper.CreateStoredFileName(folder, "My Report.pdf");
            var second = UploadHelper.CreateStoredFileName(folder, "evil.p$p");

            Assert.That(first, Does.Match(@"^[a-z0-9]{8}\.pdf$"));
            Assert.That(second, Does.Match(@"^[a-z0-9]{8}\.bin$"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
