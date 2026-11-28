using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class ShareFlowTests
{
    private readonly FakeUploader _uploader = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly CaptureResult _png = new("/tmp/shot.png", "image/png");

    private ShareFlow Flow(ClipboardContent content = ClipboardContent.Link) =>
        new(_uploader, _clipboard, new ExpirySettings(), new ClipboardSettings { Content = content });

    private BarViewModel Bar(ClipboardContent content = ClipboardContent.Link) => new(
        new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        Flow(content), new FakeScreenRecorder(), new FakeWebPageCapture());

    [Fact]
    public async Task WithNoShareHostSetUpTheImageGoesOnTheClipboardAndNothingUploads()
    {
        _uploader.IsConfigured = false;

        ShareOutcome outcome = await Flow().ShareAsync(_png);

        Assert.Null(outcome.Link);
        Assert.Equal(ClipboardPlan.LocalOnly, outcome.Copied);
        Assert.Empty(_uploader.Uploads);
        Assert.Equal(["/tmp/shot.png"], _clipboard.Images);
        Assert.Empty(_clipboard.Texts);
    }

    [Fact]
    public async Task WithNoShareHostSetUpTheBarSaysTheImageWasCopied()
    {
        _uploader.IsConfigured = false;
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.FullScreen);

        Assert.Equal(Strings.Get("notice.clipboard.local"), bar.Notice);
    }

    [Fact]
    public async Task ALinkSettingPutsOnlyTheLinkOnTheClipboard()
    {
        await Flow().ShareAsync(_png);

        Assert.Equal(["https://share.example.test/abc"], _clipboard.Texts);
        Assert.Empty(_clipboard.Images);
    }

    [Fact]
    public async Task AnImageSettingPutsOnlyTheImageOnTheClipboardAfterTheUpload()
    {
        await Flow(ClipboardContent.Image).ShareAsync(_png);

        Assert.Equal(["/tmp/shot.png"], _clipboard.Images);
        Assert.Empty(_clipboard.Texts);
        Assert.Single(_uploader.Uploads);
    }

    [Fact]
    public async Task ABothSettingPutsTheImageAndTheLinkWithTheLinkLast()
    {
        await Flow(ClipboardContent.Both).ShareAsync(_png);

        Assert.Equal(["/tmp/shot.png"], _clipboard.Images);
        Assert.Equal(["https://share.example.test/abc"], _clipboard.Texts);
        Assert.Equal(["image", "text"], _clipboard.Order);
    }

    [Fact]
    public async Task AVideoNeverGoesOnTheClipboardAsAnImage()
    {
        await Flow(ClipboardContent.Both).ShareAsync(new CaptureResult("/tmp/r.mp4", "video/mp4"));

        Assert.Empty(_clipboard.Images);
        Assert.Single(_clipboard.Texts);
    }

    [Theory]
    [InlineData(ClipboardContent.Link)]
    [InlineData(ClipboardContent.Image)]
    [InlineData(ClipboardContent.Both)]
    public async Task AFailedUploadLeavesTheClipboardAndTheFileAlone(ClipboardContent content)
    {
        string file = Path.Combine(Path.GetTempPath(), $"share-keep-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(file, [1, 2, 3]);
        _uploader.Throws = new ShareUploadException("denied");

        await Assert.ThrowsAsync<ShareUploadException>(() => Flow(content).ShareAsync(new CaptureResult(file, "image/png")));

        Assert.Empty(_clipboard.Texts);
        Assert.Empty(_clipboard.Images);
        Assert.True(File.Exists(file));
        File.Delete(file);
    }

    [Fact]
    public async Task TheBarShowsTheReasonOfAFailedUploadAndKeepsTheClipboard()
    {
        _uploader.Throws = new ShareUploadException("access denied (403)");
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.FullScreen);
        await bar.ShareLinkAsync();

        Assert.Equal("Upload failed: access denied (403). The file is kept.", bar.Notice);
        Assert.Empty(_clipboard.Texts);
        Assert.Empty(_clipboard.Images);
    }

    [Fact]
    public async Task TheBarShowsTheSettingNameOfAnInvalidSetting()
    {
        _uploader.Throws = new InvalidStorageSettingsException("Storage.Endpoint", "Setting \"Storage.Endpoint\" is empty. Set it in settings.json.");
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.FullScreen);
        await bar.ShareLinkAsync();

        Assert.Contains("Storage.Endpoint", bar.Notice);
    }

    [Fact]
    public async Task ASuccessfulCaptureShowsTheLinkCopiedNotice()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Region);
        await bar.ShareLinkAsync();

        Assert.Equal("Link copied", bar.Notice);
        Assert.Equal(["https://share.example.test/abc"], _clipboard.Texts);
    }

    [Fact]
    public async Task TheImageOnlySettingShowsTheClipboardNotice()
    {
        BarViewModel bar = Bar(ClipboardContent.Image);

        await bar.RunAsync(BarAction.Region);
        await bar.CopyImageAsync();

        Assert.Equal("Picture copied", bar.Notice);
    }
}
