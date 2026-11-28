using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Sharing;

/// <summary>A captured file goes to the share host, then the clipboard. A failed upload leaves it as it was.</summary>
public sealed class ShareFlow(
    Func<IShareUploader> currentUploader, IClipboard clipboard, Func<ExpirySettings> currentExpiry, ClipboardSettings clipboardSettings, RecentLinks? recent = null)
{
    public ShareFlow(
        IShareUploader uploader, IClipboard clipboard, ExpirySettings expiry, ClipboardSettings clipboardSettings, RecentLinks? recent = null)
        : this(() => uploader, clipboard, () => expiry, clipboardSettings, recent)
    {
    }

    /// <summary>True when a share host is set up, so a capture can get a link.</summary>
    public bool CanShare => currentUploader().IsConfigured;

    /// <summary>Puts the picture itself on the clipboard; nothing is uploaded.</summary>
    public Task CopyImageAsync(CaptureResult capture, CancellationToken cancellationToken = default) =>
        clipboard.SetImageAsync(capture.FilePath, cancellationToken);

    /// <summary>The user asked for a link: uploads and copies the link only, whatever the clipboard setting says.</summary>
    public async Task<ShareLink> CopyLinkAsync(CaptureResult capture, CancellationToken cancellationToken = default)
    {
        ShareLink link = await UploadAsync(capture, null, cancellationToken);
        await clipboard.SetTextAsync(link.Url.AbsoluteUri, cancellationToken);
        return link;
    }

    private async Task<ShareLink> UploadAsync(CaptureResult capture, Expiry? perUpload, CancellationToken cancellationToken)
    {
        ShareLink link = await currentUploader().UploadAsync(capture.FilePath, currentExpiry().Resolve(perUpload), cancellationToken);
        recent?.Record(link, capture);
        return link;
    }

    /// <summary>Shares one capture. With no share host set up nothing uploads and only the picture is copied.</summary>
    public async Task<ShareOutcome> ShareAsync(CaptureResult capture, Expiry? perUpload = null, CancellationToken cancellationToken = default)
    {
        IShareUploader uploader = currentUploader();
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

        ShareLink link = await UploadAsync(capture, perUpload, cancellationToken);
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
