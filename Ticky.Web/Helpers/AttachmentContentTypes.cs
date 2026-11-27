using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.StaticFiles;

namespace Ticky.Web.Helpers;

/// <summary>How attachments are served: images may be shown inline, everything else is only ever a download.</summary>
public static class AttachmentContentTypes
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = Create();

    /// <summary>The content type to show an image attachment inline with. False for anything that isn't an image.</summary>
    public static bool TryGetImageContentType(Attachment attachment, [NotNullWhen(true)] out string? contentType)
    {
        contentType = null;
        return AttachmentHelper.IsImage(attachment) && ContentTypes.TryGetContentType(attachment.OriginalName, out contentType);
    }

    private static FileExtensionContentTypeProvider Create()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[Constants.Attachments.AVIF_FILE_EXTENSION] = Constants.Attachments.AVIF_CONTENT_TYPE;
        return provider;
    }
}
