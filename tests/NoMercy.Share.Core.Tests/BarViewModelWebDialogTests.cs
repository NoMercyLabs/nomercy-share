using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class BarViewModelWebDialogTests
{
    private readonly FakeWebPageCapture _web = new();
    private readonly FakeUploader _uploader = new();
    private readonly FakeClipboard _clipboard = new();

    private BarViewModel Bar(IWebCaptureDialog dialog) => new(
        new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(_uploader, _clipboard, new ExpirySettings { DefaultDays = 7 }, new ClipboardSettings()),
        web: _web,
        webDialog: dialog);

    [Fact]
    public async Task TheWebPageButtonOpensTheDialogAndSharesWhatItReturns()
    {
        FakeWebDialog dialog = new(new CaptureResult("/tmp/dialog.png", "image/png"));

        BarViewModel bar = Bar(dialog);

        await bar.RunAsync(BarAction.WebPage);
        await bar.ShareLinkAsync();

        Assert.Same(_web, dialog.Capture);
        Assert.Empty(_web.Requests);
        Assert.Equal("/tmp/dialog.png", Assert.Single(_uploader.Uploads).File);
        Assert.Equal(["https://share.example.test/abc"], _clipboard.Texts);
    }

    [Fact]
    public async Task ACancelledDialogSharesNothingAndSaysNothing()
    {
        BarViewModel bar = Bar(new FakeWebDialog(new OperationCanceledException()));

        await bar.RunAsync(BarAction.WebPage);

        Assert.Empty(_uploader.Uploads);
        Assert.Null(bar.Notice);
    }

    [Fact]
    public async Task AWebCaptureFailureShowsItsOwnSentence()
    {
        BarViewModel bar = Bar(new FakeWebDialog(new WebCaptureException(WebCaptureField.None, "No Edge or Chrome was found.")));

        await bar.RunAsync(BarAction.WebPage);

        Assert.Equal("No Edge or Chrome was found.", bar.Notice);
        Assert.Empty(_uploader.Uploads);
    }

    private sealed class FakeWebDialog : IWebCaptureDialog
    {
        private readonly CaptureResult? _result;
        private readonly Exception? _throws;

        public FakeWebDialog(CaptureResult result) => _result = result;

        public FakeWebDialog(Exception throws) => _throws = throws;

        public IWebPageCapture? Capture { get; private set; }

        public Task<CaptureResult> RunAsync(IWebPageCapture capture, CancellationToken cancellationToken = default)
        {
            Capture = capture;
            return _throws is null ? Task.FromResult(_result!) : Task.FromException<CaptureResult>(_throws);
        }
    }
}
