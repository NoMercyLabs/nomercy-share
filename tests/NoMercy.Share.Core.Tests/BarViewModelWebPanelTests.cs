using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class BarViewModelWebPanelTests
{
    private readonly FakeWebPageCapture _web = new();
    private readonly FakeUploader _uploader = new();
    private readonly FakeClipboard _clipboard = new();

    private BarViewModel Bar(IWebPageCapture? web = null) => new(
        new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(_uploader, _clipboard, new ExpirySettings { DefaultDays = 7 }, new ClipboardSettings()),
        recorder: new FakeScreenRecorder(),
        web: web ?? _web,
        recent: new RecentLinks(Path.Combine(Path.GetTempPath(), $"share-panel-{Guid.NewGuid():N}.json"), new RecentSettings()),
        clipboard: _clipboard);

    private static Task Done(Task run) => run.WaitAsync(TimeSpan.FromSeconds(10));

    private static (Task Run, WebCaptureViewModel Panel) Open(BarViewModel bar)
    {
        Task run = bar.RunAsync(BarAction.WebPage);
        Assert.NotNull(bar.WebPanel);
        return (run, bar.WebPanel);
    }

    [Fact]
    public async Task TheGlobeOpensThePanelAndSharesThePictureItTakes()
    {
        _clipboard.Text = "https://example.test/articles/made-up";
        BarViewModel bar = Bar();

        (Task run, WebCaptureViewModel panel) = Open(bar);
        Assert.Equal("https://example.test/articles/made-up", panel.Address);
        Assert.Empty(_web.Requests);
        await Done(panel.CaptureAsync());
        await Done(run);
        Assert.Null(bar.WebPanel);
        await bar.ShareLinkAsync();

        Assert.Single(_web.Requests);
        Assert.Equal("/tmp/page.png", Assert.Single(_uploader.Uploads).File);
        Assert.Equal(["https://share.example.test/abc"], _clipboard.Texts);
    }

    [Fact]
    public async Task ACancelledPanelSharesNothingAndSaysNothing()
    {
        BarViewModel bar = Bar();
        (Task run, WebCaptureViewModel panel) = Open(bar);

        panel.Cancel();
        await Done(run);

        Assert.Null(bar.WebPanel);
        Assert.Null(bar.Choice);
        Assert.Null(bar.Notice);
        Assert.Empty(_uploader.Uploads);
    }

    [Fact]
    public async Task AFailureKeepsThePanelOpenWithItsOwnSentence()
    {
        _web.Throws = new WebCaptureException(WebCaptureField.None, "No Edge or Chrome was found.");
        BarViewModel bar = Bar();
        (Task run, WebCaptureViewModel panel) = Open(bar);
        panel.Address = "https://example.test/";

        await Done(panel.CaptureAsync());

        Assert.Same(panel, bar.WebPanel);
        Assert.Equal("No Edge or Chrome was found.", panel.FailureText);
        Assert.False(run.IsCompleted);
        Assert.Empty(_uploader.Uploads);
        panel.Cancel();
        await Done(run);
    }


    [Fact]
    public async Task TheCrossClosesAnOpenPanelAndTheBar()
    {
        BarViewModel bar = Bar();
        int closes = 0;
        bar.CloseRequested += (_, _) => closes++;
        (Task run, _) = Open(bar);

        await Done(bar.RunAsync(BarAction.Close));
        await Done(run);

        Assert.Null(bar.WebPanel);
        Assert.Equal(1, closes);
    }

    [Theory]
    [InlineData(BarAction.Screenshot, BarMode.Screenshot)]
    [InlineData(BarAction.Record, BarMode.Record)]
    [InlineData(BarAction.Region, BarMode.Screenshot)]
    public async Task AnotherModeOrACaptureHotkeyClosesThePanelAndRuns(BarAction action, BarMode mode)
    {
        BarViewModel bar = Bar();
        (Task run, _) = Open(bar);

        await Done(bar.RunAsync(action));
        await Done(run);

        Assert.Null(bar.WebPanel);
        Assert.Equal(mode, bar.Row.Mode);
    }

    [Fact]
    public async Task EscapeCancelsARunningCaptureAndGoesBackToScreenshotModeBeforeItHidesTheBar()
    {
        WaitingWebCapture web = new();
        BarViewModel bar = Bar(web);
        int closes = 0;
        bar.CloseRequested += (_, _) => closes++;
        (Task run, WebCaptureViewModel panel) = Open(bar);
        panel.Address = "https://example.test/";
        Task capture = panel.CaptureAsync();
        Assert.True(panel.IsBusy);

        bar.Escape();
        await Done(run);
        await Done(capture);

        Assert.True(web.Canceled);
        Assert.Null(bar.WebPanel);
        Assert.True(bar.Row.IsScreenshotMode);
        Assert.Equal(0, closes);
        bar.Escape();
        Assert.Equal(1, closes);
    }

    [Fact]
    public async Task TheRecentListAndThePanelAreNeverBothOpen()
    {
        BarViewModel bar = Bar();
        (Task first, _) = Open(bar);

        await Done(bar.RunAsync(BarAction.Recent));
        await Done(first);
        Assert.True(bar.IsRecentOpen);
        Assert.Null(bar.WebPanel);

        (Task second, WebCaptureViewModel panel) = Open(bar);
        Assert.False(bar.IsRecentOpen);
        panel.Cancel();
        await Done(second);
    }

    [Fact]
    public async Task EscapeClosesTheRecentListFirst()
    {
        BarViewModel bar = Bar();
        int closes = 0;
        bar.CloseRequested += (_, _) => closes++;
        await Done(bar.RunAsync(BarAction.Recent));

        bar.Escape();

        Assert.False(bar.IsRecentOpen);
        Assert.Equal(0, closes);
    }


    /// <summary>A page that never finishes loading, until the capture is canceled.</summary>
    private sealed class WaitingWebCapture : IWebPageCapture
    {
        public bool Canceled { get; private set; }

        public async Task<CaptureResult> CaptureAsync(WebCaptureRequest request, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<CaptureResult> taken = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                Canceled = true;
                taken.TrySetCanceled();
            });
            return await taken.Task;
        }
    }
}
