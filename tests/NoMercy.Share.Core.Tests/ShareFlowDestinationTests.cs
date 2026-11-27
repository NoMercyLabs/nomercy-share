using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class ShareFlowDestinationTests
{
    private readonly FakeUploader _uploader = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeDestination _destination = new();
    private readonly CaptureResult _png = new("/tmp/shot.png", "image/png");

    private ShareFlow Flow() =>
        new(_uploader, _clipboard, new ExpirySettings(), new ClipboardSettings(), destinations: [_destination]);

    private BarViewModel Bar() => new(
        new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        Flow(), new FakeScreenRecorder(), new FakeWebPageCapture());

    [Fact]
    public async Task TheShareLinkGoesToTheDestinationAfterTheClipboardHasIt()
    {
        int clipboardTexts = -1;
        _destination.OnSend = () => clipboardTexts = _clipboard.Texts.Count;

        await Flow().ShareAsync(_png);

        (ShareLink link, string contentType) = Assert.Single(_destination.Sent);
        Assert.Equal("https://share.example.test/abc", link.Url.AbsoluteUri);
        Assert.Equal("image/png", contentType);
        Assert.Equal(1, clipboardTexts);
    }

    [Fact]
    public async Task ALocalCaptureSendsNothing()
    {
        _uploader.IsConfigured = false;

        await Flow().ShareAsync(_png);

        Assert.Empty(_destination.Sent);
    }

    [Fact]
    public async Task ADestinationThatIsNotSetUpIsSkipped()
    {
        _destination.IsConfigured = false;

        ShareOutcome outcome = await Flow().ShareAsync(_png);

        Assert.Empty(_destination.Sent);
        Assert.Empty(outcome.DestinationFailures);
    }

    [Fact]
    public async Task AFailedSendKeepsTheClipboardLinkAndNamesTheDestination()
    {
        _destination.Throws = new LinkDestinationException("Discord answered 404");

        ShareOutcome outcome = await Flow().ShareAsync(_png);

        Assert.Equal(["https://share.example.test/abc"], _clipboard.Texts);
        Assert.NotNull(outcome.Link);
        DestinationFailure failure = Assert.Single(outcome.DestinationFailures);
        Assert.Equal(("Discord", "Discord answered 404"), (failure.Name, failure.Reason));
    }

    [Fact]
    public async Task AnUnexpectedErrorInASendNeverFailsTheShare()
    {
        _destination.Throws = new InvalidOperationException("broken");

        ShareOutcome outcome = await Flow().ShareAsync(_png);

        Assert.NotNull(outcome.Link);
        Assert.Single(outcome.DestinationFailures);
    }

    [Fact]
    public async Task TheBarSaysTheLinkWasCopiedAndThePostFailed()
    {
        _destination.Throws = new LinkDestinationException("Discord answered 404");
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.FullScreen);

        Assert.Equal("Link copied. Posting to Discord failed: Discord answered 404.", bar.Notice);
    }

    [Fact]
    public async Task ASuccessfulSendLeavesTheBarNoticeAsItWas()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.FullScreen);

        Assert.Equal(Strings.Get("notice.copied"), bar.Notice);
    }
}
