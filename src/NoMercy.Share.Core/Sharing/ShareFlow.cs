using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Sharing;

/// <summary>A captured file goes to the share host and its link goes to the clipboard.</summary>
public sealed class ShareFlow(IShareUploader uploader, IClipboard clipboard, ExpirySettings expiry)
{
    public async Task<ShareLink> ShareAsync(CaptureResult capture, Expiry? perUpload = null, CancellationToken cancellationToken = default)
    {
        ShareLink link = await uploader.UploadAsync(capture.FilePath, expiry.Resolve(perUpload), cancellationToken);
        await clipboard.SetTextAsync(link.Url.AbsoluteUri, cancellationToken);
        return link;
    }
}
