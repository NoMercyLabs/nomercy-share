using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class BarViewModelRecordingTests
{
    private readonly FakeScreenRecorder _recorder = new();
    private readonly FakeUploader _uploader = new();
    private readonly FakeClipboard _clipboard = new();
    private DateTimeOffset _now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private BarViewModel Bar() => new(
        new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(_uploader, _clipboard, new ExpirySettings(), new ClipboardSettings()),
        _recorder, new FakeWebPageCapture(), () => _now);

    [Fact]
    public async Task WhileRecordingTheRecordButtonIsCalledStopAndWhenDoneItIsCalledRecordAgain()
    {
        BarViewModel bar = Bar();
        Assert.Equal("Record the screen", bar.RecordLabel);

        await bar.RunAsync(BarAction.Record);
        Assert.Equal("Stop recording", bar.RecordLabel);

        await bar.RunAsync(BarAction.Record);
        Assert.Equal("Record the screen", bar.RecordLabel);
    }

    [Fact]
    public async Task TheElapsedTimeCountsUpInMinutesAndSecondsFromTheMomentRecordingStarted()
    {
        BarViewModel bar = Bar();
        Assert.Null(bar.ElapsedText);

        await bar.RunAsync(BarAction.Record);
        Assert.Equal("00:00", bar.ElapsedText);

        _now = _now.AddSeconds(65);
        bar.RefreshElapsed();
        Assert.Equal("01:05", bar.ElapsedText);

        _now = _now.AddSeconds(3600);
        bar.RefreshElapsed();
        Assert.Equal("61:05", bar.ElapsedText);
    }

    [Fact]
    public async Task RefreshingTheTimerRaisesAChangeSoTheBarRedraws()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);
        List<string?> changed = [];
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.RefreshElapsed();

        Assert.Equal([nameof(BarViewModel.ElapsedText)], changed);
    }

    [Fact]
    public async Task StoppingClearsTheTimerAndSharesTheVideo()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);

        await bar.RunAsync(BarAction.Record);

        Assert.Null(bar.ElapsedText);
        Assert.Equal("/tmp/recording.mp4", Assert.Single(_uploader.Uploads).File);
    }

    [Fact]
    public async Task AFailedRecordingShowsTheReasonAndLeavesTheBarUsable()
    {
        _recorder.StopThrows = new RecordingException("ddagrab: Access is denied");
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);

        await bar.RunAsync(BarAction.Record);

        Assert.Equal("Recording failed: ddagrab: Access is denied", bar.Notice);
        Assert.False(bar.IsRecording);
        Assert.Null(bar.ElapsedText);
        Assert.Empty(_uploader.Uploads);

        _recorder.StopThrows = null;
        await bar.RunAsync(BarAction.Record);
        Assert.True(bar.IsRecording);
    }

    [Fact]
    public async Task AStartThatCannotBeginShowsTheReasonAndDoesNotCountTime()
    {
        _recorder.StartThrows = new RecordingException("ffmpeg was not found");
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Record);

        Assert.Equal("Recording failed: ffmpeg was not found", bar.Notice);
        Assert.False(bar.IsRecording);
        Assert.Null(bar.ElapsedText);
    }

    [Fact]
    public async Task WhenFfmpegDiesDuringARecordingTheBarLeavesTheRecordingStateAtOnceAndShowsTheReason()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);
        List<string?> changed = [];
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _recorder.Fail("Error opening output: disk full");

        Assert.False(bar.IsRecording);
        Assert.Equal("Record the screen", bar.RecordLabel);
        Assert.Null(bar.ElapsedText);
        Assert.Equal("Recording failed: Error opening output: disk full", bar.Notice);
        Assert.Contains(nameof(BarViewModel.IsRecording), changed);
        Assert.Contains(nameof(BarViewModel.RecordLabel), changed);
        Assert.Empty(_uploader.Uploads);

        await bar.RunAsync(BarAction.Record);
        Assert.True(bar.IsRecording);
    }

    [Fact]
    public async Task AVideoOverTheCapIsSharedAndTheNoticeNamesBothSizesInMegabytes()
    {
        const long megabyte = 1024 * 1024;
        _recorder.Result = new CaptureResult("/tmp/recording.mp4", "video/mp4", 640, 360, SizeBytes: 63 * megabyte + megabyte / 2, SizeCapBytes: 50 * megabyte);
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);

        await bar.RunAsync(BarAction.Record);

        Assert.Equal("/tmp/recording.mp4", Assert.Single(_uploader.Uploads).File);
        Assert.Contains("63.5 MB", bar.Notice, StringComparison.Ordinal);
        Assert.Contains("50 MB", bar.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AVideoUnderTheCapGetsTheUsualNoticeOnly()
    {
        const long megabyte = 1024 * 1024;
        _recorder.Result = new CaptureResult("/tmp/recording.mp4", "video/mp4", 640, 360, SizeBytes: 10 * megabyte, SizeCapBytes: 50 * megabyte);
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);

        await bar.RunAsync(BarAction.Record);

        Assert.DoesNotContain("MB", bar.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACancelledRegionStartsNothingAndSaysNothing()
    {
        _recorder.StartThrows = new OperationCanceledException();
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Record);

        Assert.Null(bar.Notice);
        Assert.False(bar.IsRecording);
        Assert.Null(bar.ElapsedText);
    }
}
