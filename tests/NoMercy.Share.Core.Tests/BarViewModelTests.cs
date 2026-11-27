using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class BarViewModelTests
{
    private readonly FakeScreenCapture _screen = new();
    private readonly FakeScreenRecorder _recorder = new();
    private readonly FakeWebPageCapture _web = new();
    private readonly FakeUploader _uploader = new();
    private readonly FakeClipboard _clipboard = new();

    private BarViewModel Bar() => new(
        new CaptureFlow(_screen, new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        _recorder, _web, new ShareFlow(_uploader, _clipboard, new ExpirySettings { DefaultDays = 7 }, new ClipboardSettings()));

    [Fact]
    public void TheRegionButtonCapturesARegionUploadsItAndPutsTheLinkOnTheClipboard()
    {
        Bar().RegionCommand.Execute(null);

        Assert.Equal(["region"], _screen.Calls);
        Assert.Equal("/tmp/region.png", Assert.Single(_uploader.Uploads).File);
        Assert.Equal(["https://share.example.test/abc"], _clipboard.Texts);
        Assert.Empty(_clipboard.Images);
    }

    [Fact]
    public void TheWindowButtonCapturesAWindow()
    {
        Bar().WindowCommand.Execute(null);

        Assert.Equal(["window"], _screen.Calls);
    }

    [Fact]
    public void TheFullScreenButtonCapturesTheFullScreen()
    {
        Bar().FullScreenCommand.Execute(null);

        Assert.Equal(["fullscreen"], _screen.Calls);
    }

    [Fact]
    public void TheWebPageButtonAsksTheWebCaptureForAFullPageOfTheOpenBrowserPage()
    {
        Bar().WebPageCommand.Execute(null);

        WebCaptureRequest request = Assert.Single(_web.Requests);
        Assert.Null(request.Page);
        Assert.Equal(WebCaptureKind.FullPage, request.Kind);
    }

    [Fact]
    public void TheRecordButtonStartsAndThenStopsAndSharesTheVideo()
    {
        BarViewModel bar = Bar();

        bar.RecordCommand.Execute(null);
        Assert.True(bar.IsRecording);
        Assert.Empty(_uploader.Uploads);

        bar.RecordCommand.Execute(null);
        Assert.False(bar.IsRecording);
        Assert.Equal("/tmp/recording.mp4", Assert.Single(_uploader.Uploads).File);
    }

    [Fact]
    public void AnActionThatIsNotBuiltYetShowsAShortNoticeInsteadOfCrashing()
    {
        _screen.Throws = new NotImplementedException("later");
        BarViewModel bar = Bar();

        bar.RegionCommand.Execute(null);

        Assert.Equal("Coming in a later step", bar.Notice);
        Assert.Empty(_uploader.Uploads);
    }

    [Fact]
    public void ARecorderThatCannotStartLeavesTheBarNotRecording()
    {
        _recorder.StartThrows = new NotImplementedException("later");
        BarViewModel bar = Bar();

        bar.RecordCommand.Execute(null);

        Assert.False(bar.IsRecording);
        Assert.Equal("Coming in a later step", bar.Notice);
    }

    [Fact]
    public void AnUnexpectedFailureShowsAPlainNoticeAndKeepsTheBarAlive()
    {
        _screen.Throws = new InvalidOperationException("boom");
        BarViewModel bar = Bar();

        bar.RegionCommand.Execute(null);

        Assert.Equal("That did not work. Try again.", bar.Notice);
    }

    [Fact]
    public void RecentLinksAndSettingsAreNotBuiltYetAndSaySo()
    {
        BarViewModel bar = Bar();

        bar.RecentCommand.Execute(null);
        Assert.Equal("Coming in a later step", bar.Notice);

        bar.ClearNotice();
        Assert.Null(bar.Notice);

        bar.SettingsCommand.Execute(null);
        Assert.Equal("Coming in a later step", bar.Notice);
    }

    [Fact]
    public void TheCloseButtonAsksTheWindowToClose()
    {
        BarViewModel bar = Bar();
        int closes = 0;
        bar.CloseRequested += (_, _) => closes++;

        bar.CloseCommand.Execute(null);

        Assert.Equal(1, closes);
    }

    [Fact]
    public async Task ShareFlowUsesAnUploadsOwnExpiryWhenGiven()
    {
        ShareFlow flow = new(_uploader, _clipboard, new ExpirySettings { DefaultDays = 7 }, new ClipboardSettings());

        await flow.ShareAsync(new CaptureResult("/tmp/a.png", "image/png"), Expiry.Never);

        Assert.Equal(Expiry.Never, Assert.Single(_uploader.Uploads).Expiry);
    }
}
