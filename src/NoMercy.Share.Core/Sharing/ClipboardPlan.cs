using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Sharing;

/// <summary>What goes on the clipboard after a capture: the picture, the link, or both.</summary>
public readonly record struct ClipboardPlan(bool Image, bool Text)
{
    private const string ImageContentTypePrefix = "image/";

    /// <summary>With no share host there is no link, so only the picture is copied.</summary>
    public static ClipboardPlan LocalOnly => new(Image: true, Text: false);

    /// <summary>A video never goes on the clipboard as a picture, so its plan is the link alone.</summary>
    public static ClipboardPlan For(ClipboardContent content, string contentType)
    {
        bool isPicture = contentType.StartsWith(ImageContentTypePrefix, StringComparison.Ordinal);
        bool image = isPicture && content is ClipboardContent.Image or ClipboardContent.Both;
        bool text = content is ClipboardContent.Link or ClipboardContent.Both || !image;
        return new ClipboardPlan(image, text);
    }
}
