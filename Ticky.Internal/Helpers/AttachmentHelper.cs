namespace Ticky.Internal.Helpers;

public static class AttachmentHelper
{
    public static string GetFileTypeFromAttachment(Attachment attachment)
    {
        if (attachment.OriginalName.Contains(".doc"))
            return "DOC";
        else if (
            attachment.OriginalName.Contains(".jpg") || attachment.OriginalName.Contains(".png")
        )
            return "Image";
        else if (attachment.OriginalName.Contains(".pdf"))
            return "PDF";
        else if (attachment.OriginalName.Contains(".ppt"))
            return "PowerPoint";
        else if (attachment.OriginalName.Contains(".sql"))
            return "Script";
        else if (attachment.OriginalName.Contains(".txt"))
            return "Text";
        else if (attachment.OriginalName.Contains(".xls"))
            return "Excel";
        else if (attachment.OriginalName.Contains(".xml"))
            return "XML";
        else if (attachment.OriginalName.Contains(".zip"))
            return "Archive";

        return "Other";
    }

    public static string GetImageNameFromAttachment(Attachment attachment)
    {
        if (attachment.OriginalName.Contains(".doc"))
            return "doc.png";
        else if (attachment.OriginalName.Contains(".jpg"))
            return "jpg.png";
        else if (attachment.OriginalName.Contains(".png"))
            return "png.png";
        else if (attachment.OriginalName.Contains(".pdf"))
            return "pdf.png";
        else if (attachment.OriginalName.Contains(".ppt"))
            return "ppt.png";
        else if (attachment.OriginalName.Contains(".sql"))
            return "sql.png";
        else if (attachment.OriginalName.Contains(".txt"))
            return "txt.png";
        else if (attachment.OriginalName.Contains(".xls"))
            return "xls.png";
        else if (attachment.OriginalName.Contains(".xml"))
            return "xml.png";
        else if (attachment.OriginalName.Contains(".zip"))
            return "zip.png";

        return "txt.png";
    }
}
