using System.Diagnostics;
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

    private BarViewModel Bar(bool withRecordingAndWeb = true) => new(
        new CaptureFlow(_screen, new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(_uploader, _clipboard, new ExpirySettings { DefaultDays = 7 }, new ClipboardSettings()),
        withRecordingAndWeb ? _recorder : null,
        withRecordingAndWeb ? _web : null);

    [Fact]
    public async Task TheRegionButtonCapturesARegionUploadsItAndPutsTheLinkOnTheClipboard()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Region);
        await bar.ShareLinkAsync();

        Assert.Equal(["region"], _screen.Calls);
        Assert.Equal("/tmp/region.png", Assert.Single(_uploader.Uploads).File);
        Assert.Equal(["https://share.example.test/abc"], _clipboard.Texts);
        Assert.Empty(_clipboard.Images);
    }

    [Fact]
    public async Task TheWindowButtonCapturesAWindow()
    {
        await Bar().RunAsync(BarAction.Window);

        Assert.Equal(["window"], _screen.Calls);
    }

    [Fact]
    public async Task TheFullScreenButtonCapturesTheFullScreen()
    {
        await Bar().RunAsync(BarAction.FullScreen);

        Assert.Equal(["fullscreen"], _screen.Calls);
    }

    [Fact]
    public async Task TheWebPageButtonAsksTheWebCaptureForAFullPageOfTheOpenBrowserPage()
    {
        await Bar().RunAsync(BarAction.WebPage);

        WebCaptureRequest request = Assert.Single(_web.Requests);
        Assert.Null(request.Page);
        Assert.Equal(WebCaptureKind.FullPage, request.Kind);
    }

    [Fact]
    public async Task TheRecordButtonStartsAndThenStopsAndSharesTheVideo()
    {
        BarViewModel bar = Bar();

        await bar.OpenAndStartAsync();
        Assert.True(bar.IsRecording);
        Assert.Empty(_uploader.Uploads);

        await bar.RunAsync(BarAction.Record);
        await bar.ShareLinkAsync();
        Assert.False(bar.IsRecording);
        Assert.Equal("/tmp/recording.mp4", Assert.Single(_uploader.Uploads).File);
    }

    [Fact]
    public void TheButtonCommandRunsTheActionInItsParameter()
    {
        Bar().ActionCommand.Execute(BarAction.Window);

        Assert.Equal(["window"], _screen.Calls);
    }

    [Theory]
    [InlineData(BarAction.Record)]
    [InlineData(BarAction.WebPage)]
    public async Task AButtonWhoseFeatureIsNotBuiltSaysSoAndDoesNothingElse(BarAction action)
    {
        BarViewModel bar = Bar(withRecordingAndWeb: false);

        await bar.RunAsync(action);

        Assert.Equal("Coming in a later step", bar.Notice);
        Assert.False(bar.IsRecording);
        Assert.Empty(_uploader.Uploads);
    }

    [Fact]
    public async Task AnUnexpectedFailureShowsAPlainNoticeLogsTheExceptionAndKeepsTheBarAlive()
    {
        _screen.Throws = new InvalidOperationException("boom");
        BarViewModel bar = Bar();
        StringWriter log = new();
        using TextWriterTraceListener listener = new(log);
        Trace.Listeners.Add(listener);

        try
        {
            await bar.RunAsync(BarAction.Region);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        listener.Flush();
        Assert.Equal("That did not work. Try again.", bar.Notice);
        Assert.Contains("boom", log.ToString());
    }

    [Fact]
    public async Task RecentLinksAndSettingsAreNotBuiltYetAndSaySo()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Recent);
        Assert.Equal("Coming in a later step", bar.Notice);

        bar.ClearNotice();
        Assert.Null(bar.Notice);

        await bar.RunAsync(BarAction.Settings);
        Assert.Equal("Coming in a later step", bar.Notice);
    }

    [Fact]
    public async Task TwoCapturesWithTheSameNoticeRaiseANoticeChangeEachTime()
    {
        BarViewModel bar = Bar();
        int changes = 0;
        bar.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(BarViewModel.Notice) ? 1 : 0;

        await bar.RunAsync(BarAction.Region);
        await bar.ShareLinkAsync();
        string? first = bar.Notice;
        await bar.RunAsync(BarAction.Region);
        await bar.ShareLinkAsync();

        Assert.Equal(first, bar.Notice);
        Assert.True(changes >= 2, $"the window restarts its hide timer on a Notice change, but only {changes} change was raised");
    }

    [Fact]
    public async Task TheCloseButtonAsksTheWindowToClose()
    {
        BarViewModel bar = Bar();
        int closes = 0;
        bar.CloseRequested += (_, _) => closes++;

        await bar.RunAsync(BarAction.Close);

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
