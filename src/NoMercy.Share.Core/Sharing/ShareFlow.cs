using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Sharing;

/// <summary>A captured file goes to the share host, then the clipboard gets what the settings choose. A failed upload leaves it as it was.</summary>
public sealed class ShareFlow(IShareUploader uploader, IClipboard clipboard, ExpirySettings expiry, ClipboardSettings clipboardSettings)
{
    private const string ImageContentTypePrefix = "image/";

    /// <summary>True when the clipboard holds only the picture, not the link.</summary>
    public bool PutsOnlyTheImage(CaptureResult capture) =>
        clipboardSettings.Content == ClipboardContent.Image && capture.ContentType.StartsWith(ImageContentTypePrefix, StringComparison.Ordinal);

    /// <summary>True while no share host is set up: a capture stays local and only its image goes on the clipboard.</summary>
    public bool IsLocalOnly => !uploader.IsConfigured;

    /// <summary>Uploads and copies what the settings choose; returns null when no share host is set up.</summary>
    public async Task<ShareLink?> ShareAsync(CaptureResult capture, Expiry? perUpload = null, CancellationToken cancellationToken = default)
    {
        if (IsLocalOnly)
        {
            await clipboard.SetImageAsync(capture.FilePath, cancellationToken);
            return null;
        }

        uploader.Validate();
        ShareLink link = await uploader.UploadAsync(capture.FilePath, expiry.Resolve(perUpload), cancellationToken);

        bool image = clipboardSettings.Content is ClipboardContent.Image or ClipboardContent.Both
            && capture.ContentType.StartsWith(ImageContentTypePrefix, StringComparison.Ordinal);
        bool text = clipboardSettings.Content is ClipboardContent.Link or ClipboardContent.Both || !image;

        if (image)
        {
            await clipboard.SetImageAsync(capture.FilePath, cancellationToken);
        }

        if (text)
        {
            await clipboard.SetTextAsync(link.Url.AbsoluteUri, cancellationToken);
        }

        return link;
    }
}
