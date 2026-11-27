using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

/// <summary>Record without ffmpeg on the PC: one yes-or-no question on the bar, and yes downloads the NoMercy build.</summary>
public sealed class BarViewModelFfmpegTests
{
    private readonly FakeScreenRecorder _recorder = new();
    private readonly FakeFfmpegInstall _ffmpeg = new();

    private BarViewModel Bar() => new(
        new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings { DefaultDays = 7 }, new ClipboardSettings()),
        recorder: _recorder,
        ffmpeg: _ffmpeg);

    [Fact]
    public async Task RecordWithoutFfmpegAsksToDownloadItAndRecordsNothing()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Record);

        Assert.Equal(Strings.Get("ask.ffmpeg"), bar.Ask?.Text);
        Assert.Equal(Strings.Get("ask.download"), bar.Ask?.YesLabel);
        Assert.False(_recorder.IsRecording);
    }

    [Fact]
    public async Task YesDownloadsItAndSaysItIsReady()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);

        bar.AskYesCommand.Execute(null);
        await _ffmpeg.Finished;

        Assert.Equal(1, _ffmpeg.Downloads);
        Assert.Null(bar.Ask);
        Assert.Equal(Strings.Get("notice.ffmpeg.ready"), bar.Notice);
    }

    [Fact]
    public async Task ADownloadThatFailsSaysWhy()
    {
        _ffmpeg.Fails = new IOException("the checksum does not match");
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);

        bar.AskYesCommand.Execute(null);
        await _ffmpeg.Finished;

        Assert.Equal(Strings.Get("notice.ffmpeg.failed", "the checksum does not match"), bar.Notice);
    }

    [Fact]
    public async Task NotNowClosesTheQuestionAndDownloadsNothing()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);

        bar.AskNoCommand.Execute(null);

        Assert.Null(bar.Ask);
        Assert.Equal(0, _ffmpeg.Downloads);
    }

    [Fact]
    public async Task WithFfmpegFoundTheRecordingStartsWithoutAQuestion()
    {
        _ffmpeg.Found = true;
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Record);

        Assert.Null(bar.Ask);
        Assert.True(_recorder.IsRecording);
    }

    private sealed class FakeFfmpegInstall : IFfmpegInstall
    {
        private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Found { get; set; }

        public Exception? Fails { get; set; }

        public int Downloads { get; private set; }

        public Task Finished => _finished.Task;

        public bool IsFound() => Found;

        public Task DownloadAsync(IProgress<double> progress, CancellationToken cancellationToken = default)
        {
            Downloads++;
            progress.Report(0.5);
            if (Fails is { } error)
            {
                _finished.SetResult();
                return Task.FromException(error);
            }

            Found = true;
            _finished.SetResult();
            return Task.CompletedTask;
        }
    }
}
