using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

/// <summary>After a capture the user chooses what happens with it; nothing is uploaded before that.</summary>
public sealed class BarViewModelChoiceTests
{
    private readonly FakeScreenCapture _screen = new();
    private readonly FakeUploader _uploader = new();
    private readonly FakeClipboard _clipboard = new();

    private BarViewModel Bar(ClipboardContent content = ClipboardContent.Link) => new(
        new CaptureFlow(_screen, new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(_uploader, _clipboard, new ExpirySettings(), new ClipboardSettings { Content = content }),
        new FakeScreenRecorder());

    [Fact]
    public async Task AScreenshotWaitsForTheChoiceAndUploadsAndCopiesNothing()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Region);

        Assert.NotNull(bar.Choice);
        Assert.True(bar.Choice.CanCopyImage);
        Assert.True(bar.Choice.CanShareLink);
        Assert.Empty(_uploader.Uploads);
        Assert.Empty(_clipboard.Texts);
        Assert.Empty(_clipboard.Images);
    }

    [Fact]
    public async Task CopyImagePutsThePictureOnTheClipboardAndUploadsNothing()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Region);

        await bar.CopyImageAsync();

        Assert.Equal(["/tmp/region.png"], _clipboard.Images);
        Assert.Empty(_clipboard.Texts);
        Assert.Empty(_uploader.Uploads);
        Assert.Null(bar.Choice);
        Assert.Equal("Picture copied", bar.Notice);
    }

    [Theory]
    [InlineData(ClipboardContent.Link)]
    [InlineData(ClipboardContent.Image)]
    [InlineData(ClipboardContent.Both)]
    public async Task CopyLinkUploadsAndCopiesOnlyTheLink(ClipboardContent setting)
    {
        BarViewModel bar = Bar(setting);
        await bar.RunAsync(BarAction.Region);

        await bar.ShareLinkAsync();

        Assert.Equal("/tmp/region.png", Assert.Single(_uploader.Uploads).File);
        Assert.Equal(["https://share.example.test/abc"], _clipboard.Texts);
        Assert.Empty(_clipboard.Images);
        Assert.Null(bar.Choice);
    }

    [Fact]
    public async Task AVideoOffersTheLinkButNoPictureCopy()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);

        await bar.RunAsync(BarAction.Record);

        Assert.NotNull(bar.Choice);
        Assert.False(bar.Choice.CanCopyImage);
        Assert.True(bar.Choice.CanShareLink);
    }

    [Fact]
    public async Task AFailedUploadKeepsTheChoiceSoThePictureCanStillBeCopied()
    {
        _uploader.Throws = new ShareUploadException("no network");
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Region);

        await bar.ShareLinkAsync();

        Assert.NotNull(bar.Choice);
        Assert.Contains("no network", bar.Notice, StringComparison.Ordinal);
        await bar.CopyImageAsync();
        Assert.Equal(["/tmp/region.png"], _clipboard.Images);
    }

    [Fact]
    public async Task NotNowDropsTheChoiceAndTouchesNothing()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Region);

        bar.DismissChoiceCommand.Execute(null);

        Assert.Null(bar.Choice);
        Assert.Empty(_uploader.Uploads);
        Assert.Empty(_clipboard.Images);
    }

    [Fact]
    public async Task WithNoShareHostThePictureIsCopiedAtOnceAsThereIsNothingToChoose()
    {
        _uploader.IsConfigured = false;
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Region);

        Assert.Null(bar.Choice);
        Assert.Equal(["/tmp/region.png"], _clipboard.Images);
    }
}
