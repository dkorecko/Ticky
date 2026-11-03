using System.Net;

namespace Ticky.Units;

public class AvatarServiceTest
{
    private static readonly byte[] PNG_BYTES = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
    );

    private readonly List<string> _createdFiles = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var file in _createdFiles.Where(File.Exists))
            File.Delete(file);
    }

    [Test]
    public async Task FetchAvatar_HostileDisplayName_StaysInFolderWithRandomName()
    {
        var handler = new StubHandler();
        var service = new AvatarService(new HttpClient(handler));
        var folder = Path.GetFullPath(Ticky.Base.Constants.SAVE_UPLOADED_IMAGES_PATH);
        var escapeTarget = Path.GetFullPath(Path.Combine(folder, "../../escape.png"));

        var fileName = await service.FetchAvatarAsync("../../escape");
        var savedPath = Path.Combine(folder, fileName);
        _createdFiles.Add(savedPath);

        Assert.That(fileName, Does.Match(@"^generated-[a-z0-9]{8}\.png$"));
        Assert.That(AvatarService.IsGenerated(fileName), Is.True);
        Assert.That(File.ReadAllBytes(savedPath), Is.EqualTo(PNG_BYTES));
        Assert.That(File.Exists(escapeTarget), Is.False);
        Assert.That(handler.RequestedUrl, Does.Contain("name=..%2F..%2Fescape"));
    }

    [Test]
    public async Task FetchAvatar_SameDisplayName_GetsSeparateFiles()
    {
        var service = new AvatarService(new HttpClient(new StubHandler()));
        var folder = Path.GetFullPath(Ticky.Base.Constants.SAVE_UPLOADED_IMAGES_PATH);

        var first = await service.FetchAvatarAsync("Same Name");
        var second = await service.FetchAvatarAsync("Same Name");
        _createdFiles.Add(Path.Combine(folder, first));
        _createdFiles.Add(Path.Combine(folder, second));

        Assert.That(first, Is.Not.EqualTo(second));
    }

    private class StubHandler : HttpMessageHandler
    {
        public string? RequestedUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrl = request.RequestUri?.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(PNG_BYTES) });
        }
    }
}
