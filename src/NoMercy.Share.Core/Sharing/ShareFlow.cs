using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Sharing;

/// <summary>A captured file goes to the share host, then the clipboard gets what the settings choose. A failed upload leaves it as it was.</summary>
public sealed class ShareFlow(IShareUploader uploader, IClipboard clipboard, ExpirySettings expiry, ClipboardSettings clipboardSettings, RecentLinks? recent = null)
{
    /// <summary>Shares one capture. With no share host set up nothing uploads and only the picture is copied.</summary>
    public async Task<ShareOutcome> ShareAsync(CaptureResult capture, Expiry? perUpload = null, CancellationToken cancellationToken = default)
    {
        if (!uploader.IsConfigured)
        {
            ClipboardPlan local = ClipboardPlan.Local(capture.ContentType);
            if (local.Image)
            {
                await clipboard.SetImageAsync(capture.FilePath, cancellationToken);
            }
            else
            {
                await clipboard.SetTextAsync(capture.FilePath, cancellationToken);
            }

            return new ShareOutcome(null, local);
        }

        ShareLink link = await uploader.UploadAsync(capture.FilePath, expiry.Resolve(perUpload), cancellationToken);
        recent?.Record(link, capture);
        ClipboardPlan plan = ClipboardPlan.For(clipboardSettings.Content, capture.ContentType);
        if (plan.Image)
        {
            await clipboard.SetImageAsync(capture.FilePath, cancellationToken);
        }

        if (plan.Text)
        {
            await clipboard.SetTextAsync(link.Url.AbsoluteUri, cancellationToken);
        }

        return new ShareOutcome(link, plan);
    }
}
