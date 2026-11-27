using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class CaptureFlowTests
{
    private readonly FakeScreenCapture _screen = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeCaptureHost _host = new();
    private readonly List<TimeSpan> _delays = [];

    private CaptureFlow Flow(int hideDelay = 150) => new(_screen, _host, new CaptureSettings { HideDelayMilliseconds = hideDelay }, (span, _) =>
    {
        _delays.Add(span);
        return Task.CompletedTask;
    });

    private BarViewModel Bar(ClipboardContent content = ClipboardContent.Image) => new(Flow(),
        new ShareFlow(new FakeUploader(), _clipboard, new ExpirySettings(), new ClipboardSettings { Content = content }));

    [Theory]
    [InlineData(CaptureKind.Region, "region")]
    [InlineData(CaptureKind.Window, "window")]
    [InlineData(CaptureKind.FullScreen, "fullscreen")]
    public async Task EachKindCapturesAndLeavesTheClipboardToTheShareFlow(CaptureKind kind, string call)
    {
        CaptureResult result = await Flow().RunAsync(kind);

        Assert.Equal([call], _screen.Calls);
        Assert.Equal($"/tmp/{call}.png", result.FilePath);
        Assert.Empty(_clipboard.Images);
        Assert.Empty(_clipboard.Texts);
    }

    [Fact]
    public async Task TheBarIsHiddenDuringTheCaptureAndComesBackAfter()
    {
        bool hiddenDuringCapture = false;
        _screen.OnCapture = () => hiddenDuringCapture = _host.Hidden;

        await Flow().RunAsync(CaptureKind.FullScreen);

        Assert.True(hiddenDuringCapture);
        Assert.Equal(["hide", "show"], _host.Events);
    }

    [Fact]
    public async Task TheHideDelayIsASettingAndIsWaitedBeforeTheCapture()
    {
        await Flow(hideDelay: 321).RunAsync(CaptureKind.FullScreen);

        Assert.Equal([TimeSpan.FromMilliseconds(321)], _delays);
    }

    [Fact]
    public async Task ACancelledCaptureLeavesTheClipboardAloneAndBringsTheBarBack()
    {
        _screen.Throws = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Flow().RunAsync(CaptureKind.Region));

        Assert.Empty(_clipboard.Images);
        Assert.Empty(_clipboard.Texts);
        Assert.Equal(["hide", "show"], _host.Events);
    }

    [Fact]
    public async Task AFailedCaptureAlsoBringsTheBarBack()
    {
        _screen.Throws = new InvalidOperationException("no window");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Flow().RunAsync(CaptureKind.Window));

        Assert.Equal(["hide", "show"], _host.Events);
        Assert.Empty(_clipboard.Images);
    }

    [Theory]
    [InlineData(BarAction.Region, "region")]
    [InlineData(BarAction.Window, "window")]
    [InlineData(BarAction.FullScreen, "fullscreen")]
    public async Task TheButtonCapturesPutsTheImageOnTheClipboardAndShowsTheNoticeWhenTheSettingIsImage(BarAction action, string call)
    {
        BarViewModel bar = Bar(ClipboardContent.Image);

        await bar.RunAsync(action);

        Assert.Equal([$"/tmp/{call}.png"], _clipboard.Images);
        Assert.Equal("Copied to clipboard", bar.Notice);
    }

    [Fact]
    public async Task EscapeInTheRegionOverlayDoesNothing()
    {
        _screen.Throws = new OperationCanceledException();
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Region);

        Assert.Null(bar.Notice);
        Assert.Empty(_clipboard.Images);
        Assert.Equal(["hide", "show"], _host.Events);
    }

    [Fact]
    public async Task AFailedCaptureShowsThePlainFailedNotice()
    {
        _screen.Throws = new InvalidOperationException("boom");
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Window);

        Assert.Equal("That did not work. Try again.", bar.Notice);
        Assert.Empty(_clipboard.Images);
    }
}
