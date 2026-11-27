using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class ShareFlowLocalVideoTests
{
    private readonly FakeUploader _uploader = new() { IsConfigured = false };
    private readonly FakeClipboard _clipboard = new();

    private ShareFlow Flow() => new(_uploader, _clipboard, new ExpirySettings(), new ClipboardSettings());

    [Fact]
    public async Task WithNoShareHostSetUpAVideoPutsItsFilePathOnTheClipboardNotAPicture()
    {
        ShareOutcome outcome = await Flow().ShareAsync(new CaptureResult("/tmp/recording.mp4", "video/mp4"));

        Assert.Null(outcome.Link);
        Assert.Equal(new ClipboardPlan(Image: false, Text: true), outcome.Copied);
        Assert.Equal(["/tmp/recording.mp4"], _clipboard.Texts);
        Assert.Empty(_clipboard.Images);
        Assert.Empty(_uploader.Uploads);
    }
}
