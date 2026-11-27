using System.Diagnostics;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// Records a region of one monitor with ffmpeg's Desktop Duplication filter, then squeezes the file under the size cap.
/// The picture is silent: audio arrives in step 5b, so <see cref="RecordingOptions.IncludeAudio"/> is not read yet.
/// </summary>
public sealed class FfmpegScreenRecorder(CaptureFiles files, IRegionSelector selector, RecordingSettings settings) : IScreenRecorder
{
    private const string RawExtension = ".mkv";

    private const string FinishedExtension = ".mp4";

    private FfmpegProcess? _recording;
    private string? _ffmpeg;
    private string? _rawPath;
    private RecordingTarget _target;
    private Stopwatch _clock = new();

    public bool IsRecording => _recording is not null;

    /// <summary>The user picks the region; Escape cancels and nothing starts.</summary>
    public async Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default)
    {
        string ffmpeg = FfmpegLocator.Find(settings.FfmpegPath, Environment.GetEnvironmentVariable("PATH"))
            ?? throw new RecordingException(Strings.Get("error.ffmpeg.missing"));
        ScreenFrame frozen = await Task.Run(WindowsScreenCapture.FrozenVirtualScreen, cancellationToken);
        if (await selector.SelectAsync(frozen, cancellationToken) is not { IsEmpty: false } region)
        {
            throw new OperationCanceledException("The region selection was cancelled.");
        }

        IReadOnlyList<ScreenRect> monitors;
        using (DpiScope.PerMonitor())
        {
            monitors = MonitorList.Current();
        }

        _target = RecordingTarget.For(region, monitors);
        _rawPath = files.NewPath(RawExtension);
        _ffmpeg = ffmpeg;
        _recording = FfmpegProcess.Start(ffmpeg, FfmpegArguments.Record(_target, settings.FrameRate, _rawPath));
        _clock = Stopwatch.StartNew();
    }

    /// <summary>Stops the recording, squeezes it to an mp4 and removes the raw file. A failure keeps the raw file and says why.</summary>
    public async Task<CaptureResult> StopAsync(CancellationToken cancellationToken = default)
    {
        FfmpegProcess recording = _recording ?? throw new InvalidOperationException("No recording is running.");
        string raw = _rawPath!;
        double seconds = _clock.Elapsed.TotalSeconds;
        _recording = null;
        using (recording)
        {
            await recording.RequestStopAsync();
            int exit = await recording.WaitAsync(TimeSpan.FromSeconds(settings.StopTimeoutSeconds), cancellationToken);
            if (exit != 0)
            {
                throw recording.Failure(exit);
            }
        }

        VideoPlan plan = VideoBudget.Plan(seconds, _target.Width, _target.Height, settings);
        string mp4 = files.NewPath(FinishedExtension);
        using (FfmpegProcess squeeze = FfmpegProcess.Start(_ffmpeg!, FfmpegArguments.Compress(raw, mp4, plan)))
        {
            int exit = await squeeze.WaitAsync(TimeSpan.FromSeconds(settings.CompressTimeoutSeconds), cancellationToken);
            if (exit != 0)
            {
                throw squeeze.Failure(exit);
            }
        }

        File.Delete(raw);
        return new CaptureResult(mp4, "video/mp4", plan.Width, plan.Height);
    }
}
