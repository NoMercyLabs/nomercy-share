using System.Diagnostics;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// Records a region of one monitor with ffmpeg's Desktop Duplication filter, then squeezes the file under the size cap.
/// The sound switches in the settings decide whether system sound and the microphone are recorded to WAV files beside it.
/// </summary>
public sealed class FfmpegScreenRecorder(CaptureFiles files, IRegionSelector selector, Func<RecordingSettings> current) : IScreenRecorder
{
    private const string RawExtension = ".mkv";

    private const string FinishedExtension = ".mp4";

    private const string SoundExtension = ".wav";

    private static readonly TimeSpan FramePoll = TimeSpan.FromMilliseconds(5);

    private FfmpegProcess? _recording;
    private RecordingSound? _sound;
    private string? _ffmpeg;
    private string? _rawPath;
    private RecordingTarget _target;
    private Stopwatch _clock = new();
    private RecordingSettings _settings = current();

    public FfmpegScreenRecorder(CaptureFiles files, IRegionSelector selector, RecordingSettings settings)
        : this(files, selector, () => settings)
    {
    }

    public bool IsRecording => _recording is not null;

    public string? StartNotice { get; private set; }

    /// <summary>The sound files of the recording that runs, or of the last one; their offsets are from the first video frame.</summary>
    internal IReadOnlyList<AudioTrack> SoundTracks => _sound?.Tracks ?? [];

    public event EventHandler<string>? Failed;

    /// <summary>The user picks the region; Escape cancels and nothing starts.</summary>
    public async Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default)
    {
        _settings = current();
        string ffmpeg = FfmpegLocator.Find(_settings.FfmpegPath, Environment.GetEnvironmentVariable("PATH"))
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
        FfmpegProcess recording = FfmpegProcess.Start(ffmpeg, FfmpegArguments.Record(_target, _settings.FrameRate, _rawPath, flushEachPacket: _settings.Audio.Any));
        _clock = Stopwatch.StartNew();
        _sound = null;
        StartNotice = null;
        if (_settings.Audio.Any)
        {
            long firstFrame = await FirstFrameAsync(recording, _rawPath);
            _sound = RecordingSound.Start(_settings.Audio, () => files.NewPath(SoundExtension), firstFrame);
            StartNotice = _sound.Notice;
        }

        if (await recording.ExitsWithinAsync(TimeSpan.FromMilliseconds(_settings.StartupCheckMilliseconds)))
        {
            using (recording)
            {
                DeleteQuietly(_rawPath);
                await DiscardSoundAsync();
                throw recording.EndedByItself();
            }
        }

        _recording = recording;
        _ = WatchAsync(recording, _rawPath);
    }

    /// <summary>The moment ffmpeg has written its first bytes, which it does when the first frame arrives; at the wait limit it is just now.</summary>
    private async Task<long> FirstFrameAsync(FfmpegProcess recording, string raw)
    {
        TimeSpan limit = TimeSpan.FromMilliseconds(_settings.FirstFrameWaitMilliseconds);
        while (!recording.HasExited && _clock.Elapsed < limit && new FileInfo(raw) is not { Exists: true, Length: > 0 })
        {
            await Task.Delay(FramePoll);
        }

        return Stopwatch.GetTimestamp();
    }

    private async Task DiscardSoundAsync()
    {
        RecordingSound? sound = Interlocked.Exchange(ref _sound, null);
        if (sound is not null)
        {
            await sound.DiscardAsync();
        }
    }

    /// <summary>Ends the recording state when ffmpeg quits without a stop: the half-written file goes and <see cref="Failed"/> says why.</summary>
    private async Task WatchAsync(FfmpegProcess recording, string raw)
    {
        await recording.ExitedAsync();
        if (Interlocked.CompareExchange(ref _recording, null, recording) != recording)
        {
            return; // StopAsync took the recording and reads the exit itself
        }

        string reason = recording.EndedByItself().Message;
        recording.Dispose();
        DeleteQuietly(raw);
        await DiscardSoundAsync();
        Failed?.Invoke(this, reason);
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A file that cannot go now is left for the user; the failure notice matters more.
        }
    }

    /// <summary>Stops the recording, squeezes it to an mp4 and removes the raw file. A failure keeps the raw file and says why.</summary>
    public async Task<CaptureResult> StopAsync(CancellationToken cancellationToken = default)
    {
        FfmpegProcess recording = Interlocked.Exchange(ref _recording, null) ?? throw new InvalidOperationException("No recording is running.");
        string raw = _rawPath!;
        double seconds = _clock.Elapsed.TotalSeconds;
        RecordingSound? sound = _sound;
        using (recording)
        {
            await recording.RequestStopAsync();
            Task soundStopped = sound?.StopAsync() ?? Task.CompletedTask; // an idle loopback takes seconds to notice the stop
            int exit = await recording.WaitAsync(TimeSpan.FromSeconds(_settings.StopTimeoutSeconds), cancellationToken);
            await soundStopped;
            if (exit != 0)
            {
                throw recording.Failure(exit);
            }
        }

        IReadOnlyList<AudioTrack> tracks = sound?.Tracks ?? [];
        VideoPlan plan = VideoBudget.Plan(seconds, _target.Width, _target.Height, _settings, includeAudio: tracks.Count > 0);
        string mp4 = files.NewPath(FinishedExtension);
        SqueezeOutcome outcome = await SizeCapRetry.RunAsync(plan, _settings, async attempt =>
        {
            await SqueezeAsync(raw, mp4, attempt, tracks, cancellationToken);
            return new FileInfo(mp4).Length;
        });

        File.Delete(raw);
        sound?.Delete();
        long capBytes = (long)(_settings.MaxSizeMegabytes * 1024 * 1024);
        return new CaptureResult(mp4, "video/mp4", outcome.Plan.Width, outcome.Plan.Height, outcome.Bytes, capBytes);
    }

    /// <summary>One squeeze pass with the chosen encoder. A GPU encode that fails is done again on the CPU, so a driver problem never loses the recording. A retry writes over the file of the pass before it.</summary>
    private async Task SqueezeAsync(string raw, string mp4, VideoPlan plan, IReadOnlyList<AudioTrack> tracks, CancellationToken cancellationToken)
    {
        VideoEncoder encoder = VideoEncoder.From(_settings);
        if (await EncodeAsync(raw, mp4, plan, tracks, encoder, cancellationToken) is { } failure)
        {
            if (encoder.IsCpu || await EncodeAsync(raw, mp4, plan, tracks, encoder.OnCpu(), cancellationToken) is not null)
            {
                throw failure;
            }
        }
    }

    /// <summary>One encode; null when it worked, else the failure that says why.</summary>
    private async Task<RecordingException?> EncodeAsync(string raw, string mp4, VideoPlan plan, IReadOnlyList<AudioTrack> tracks, VideoEncoder encoder, CancellationToken cancellationToken)
    {
        using FfmpegProcess squeeze = FfmpegProcess.Start(_ffmpeg!, FfmpegArguments.Compress(raw, mp4, plan, tracks, _settings.AudioBitrateKbps, encoder));
        int exit = await squeeze.WaitAsync(TimeSpan.FromSeconds(_settings.CompressTimeoutSeconds), cancellationToken);
        return exit == 0 ? null : squeeze.Failure(exit);
    }
}
