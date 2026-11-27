using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Platform.Windows;
using Xunit.Abstractions;

namespace NoMercy.Share.Windows.Tests;

internal static class SoundDevices
{
    public static bool Has(DataFlow flow)
    {
        try
        {
            using MMDeviceEnumerator devices = new();
            using MMDevice device = devices.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            return device.State == DeviceState.Active;
        }
        catch (COMException)
        {
            return false;
        }
    }
}

/// <summary>A recording fact that also needs a default sound output; the loopback has nothing to read without one.</summary>
public sealed class SystemSoundFactAttribute : FactAttribute
{
    public SystemSoundFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive || Desktop.VirtualScreen().Width <= 0)
        {
            Skip = "No interactive Windows desktop on this machine.";
        }
        else if (FfmpegTools.Ffmpeg() is null || FfmpegTools.Ffprobe() is null)
        {
            Skip = "ffmpeg and ffprobe were not found.";
        }
        else if (!SoundDevices.Has(DataFlow.Render))
        {
            Skip = "No default sound output on this machine.";
        }
    }
}

/// <summary>A recording fact that records the room, so it runs only when NOMERCY_SHARE_MIC_TEST=1 asks for it.</summary>
public class MicrophoneFactAttribute : FactAttribute
{
    public const string OptInVariable = "NOMERCY_SHARE_MIC_TEST";

    public MicrophoneFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
        {
            Skip = $"Records the microphone; set {OptInVariable}=1 to run it.";
        }
        else if (!OperatingSystem.IsWindows() || !Environment.UserInteractive || Desktop.VirtualScreen().Width <= 0)
        {
            Skip = "No interactive Windows desktop on this machine.";
        }
        else if (FfmpegTools.Ffmpeg() is null || FfmpegTools.Ffprobe() is null)
        {
            Skip = "ffmpeg and ffprobe were not found.";
        }
        else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        {
            Skip = "A CI machine has no microphone to rely on.";
        }
        else if (!SoundDevices.Has(DataFlow.Capture))
        {
            Skip = "No default microphone on this machine.";
        }
    }
}

/// <summary>A microphone fact that also needs a default sound output, for the recording that mixes both.</summary>
public sealed class MicrophoneAndSystemSoundFactAttribute : MicrophoneFactAttribute
{
    public MicrophoneAndSystemSoundFactAttribute()
    {
        if (Skip is null && !SoundDevices.Has(DataFlow.Render))
        {
            Skip = "No default sound output on this machine.";
        }
    }
}

[Collection(RecordingCollection.Name)]
public sealed class SoundRecordingTests(ITestOutputHelper output) : IDisposable
{
    private const double ToneGain = 0.05; // low on purpose: a person may be in the room
    private const double SilenceFloorDb = -60;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-sound-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [SystemSoundFact]
    public async Task TheSystemSoundOfTwoSecondsEndsUpAsAnAacStreamNextToTheVideoAndIsNotSilent()
    {
        FfmpegScreenRecorder recorder = Recorder(new AudioSettings { SystemSound = true });

        await recorder.StartAsync(new RecordingOptions());
        CaptureResult result;
        using (WaveOutEvent output = new())
        {
            SignalGenerator tone = new(44100, 1) { Type = SignalGeneratorType.Sin, Frequency = 440, Gain = ToneGain };
            output.Init(new SampleToWaveProvider16(new OffsetSampleProvider(tone) { Take = TimeSpan.FromSeconds(2) }));
            output.Play();
            await Task.Delay(TimeSpan.FromSeconds(2));
            result = await recorder.StopAsync();
        }

        Assert.Null(recorder.StartNotice);
        Assert.InRange(Assert.Single(recorder.SoundTracks).OffsetSeconds, 0, 1);
        Assert.Equal([result.FilePath], Directory.GetFiles(_folder));
        using JsonDocument probe = JsonDocument.Parse(Probe(result.FilePath));
        string[] kinds = [.. probe.RootElement.GetProperty("streams").EnumerateArray().Select(stream => stream.GetProperty("codec_name").GetString()!)];
        Assert.Equal(["h264", "aac"], kinds);
        double seconds = double.Parse(probe.RootElement.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        Assert.InRange(seconds, 1.5, 3);
        double mean = MeanVolumeDb(result.FilePath);
        output.WriteLine($"sound offset {recorder.SoundTracks.Single().OffsetSeconds:F3} s, duration {seconds:F2} s, mean volume {mean} dB, streams {string.Join(",", kinds)}");
        Assert.True(mean > SilenceFloorDb, $"mean volume {mean} dB");
    }

    [MicrophoneFact]
    public async Task TheMicrophoneEndsUpAsAnAacStreamNextToTheVideoWhateverIsHeard()
    {
        FfmpegScreenRecorder recorder = Recorder(new AudioSettings { Microphone = true });

        await recorder.StartAsync(new RecordingOptions());
        await Task.Delay(TimeSpan.FromSeconds(2));
        CaptureResult result = await recorder.StopAsync();

        Assert.Null(recorder.StartNotice);
        Assert.Equal([result.FilePath], Directory.GetFiles(_folder));
        using JsonDocument probe = JsonDocument.Parse(Probe(result.FilePath));
        string[] kinds = [.. probe.RootElement.GetProperty("streams").EnumerateArray().Select(stream => stream.GetProperty("codec_name").GetString()!)];
        Assert.Equal(["h264", "aac"], kinds);
    }

    [MicrophoneAndSystemSoundFact]
    public async Task BothSoundsAreMixedIntoOneAacStreamWithTheTone()
    {
        FfmpegScreenRecorder recorder = Recorder(new AudioSettings { SystemSound = true, Microphone = true });

        await recorder.StartAsync(new RecordingOptions());
        CaptureResult result;
        using (WaveOutEvent output = new())
        {
            SignalGenerator tone = new(44100, 1) { Type = SignalGeneratorType.Sin, Frequency = 440, Gain = ToneGain };
            output.Init(new SampleToWaveProvider16(new OffsetSampleProvider(tone) { Take = TimeSpan.FromSeconds(2) }));
            output.Play();
            await Task.Delay(TimeSpan.FromSeconds(2));
            result = await recorder.StopAsync();
        }

        Assert.Null(recorder.StartNotice);
        Assert.Equal(2, recorder.SoundTracks.Count);
        Assert.Equal([result.FilePath], Directory.GetFiles(_folder));
        using JsonDocument probe = JsonDocument.Parse(Probe(result.FilePath));
        string[] kinds = [.. probe.RootElement.GetProperty("streams").EnumerateArray().Select(stream => stream.GetProperty("codec_name").GetString()!)];
        Assert.Equal(["h264", "aac"], kinds);
        double seconds = double.Parse(probe.RootElement.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        Assert.InRange(seconds, 1.5, 3);
        Assert.True(MeanVolumeDb(result.FilePath) > SilenceFloorDb);
    }

    [RecordingFact]
    public async Task WithTheSwitchesOffTheVideoHasNoSoundStreamAndNoNoticeIsGiven()
    {
        FfmpegScreenRecorder recorder = Recorder(new AudioSettings());

        await recorder.StartAsync(new RecordingOptions());
        await Task.Delay(TimeSpan.FromSeconds(1));
        CaptureResult result = await recorder.StopAsync();

        Assert.Null(recorder.StartNotice);
        using JsonDocument probe = JsonDocument.Parse(Probe(result.FilePath));
        Assert.Equal(["h264"], probe.RootElement.GetProperty("streams").EnumerateArray().Select(stream => stream.GetProperty("codec_name").GetString()!));
    }

    private FfmpegScreenRecorder Recorder(AudioSettings audio)
    {
        ScreenRect screen = Desktop.VirtualScreen();
        RecordingSettings recording = new() { FfmpegPath = FfmpegTools.Ffmpeg()!, StartupCheckMilliseconds = 200, Audio = audio };
        return new FfmpegScreenRecorder(
            new CaptureFiles(new CaptureSettings { Folder = _folder }, "unused", () => DateTimeOffset.UtcNow),
            new FixedRegion(new ScreenRect(screen.X + 10, screen.Y + 20, 320, 240)), recording);
    }

    private static string Probe(string file)
    {
        ProcessStartInfo start = new(FfmpegTools.Ffprobe()!) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in new[] { "-v", "error", "-show_entries", "stream=codec_name:format=duration", "-of", "json", file })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return output;
    }

    /// <summary>The mean level ffmpeg's volumedetect filter reports for the sound of the file.</summary>
    private static double MeanVolumeDb(string file)
    {
        ProcessStartInfo start = new(FfmpegTools.Ffmpeg()!) { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in new[] { "-hide_banner", "-nostdin", "-i", file, "-vn", "-af", "volumedetect", "-f", "null", "-" })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        const string Key = "mean_volume: ";
        int at = log.IndexOf(Key, StringComparison.Ordinal);
        Assert.True(at >= 0, log);
        string rest = log[(at + Key.Length)..];
        return double.Parse(rest[..rest.IndexOf(' ', StringComparison.Ordinal)], CultureInfo.InvariantCulture);
    }

    private sealed class FixedRegion(ScreenRect? chosen) : IRegionSelector
    {
        public Task<ScreenRect?> SelectAsync(ScreenFrame frozen, CancellationToken cancellationToken = default) => Task.FromResult(chosen);
    }
}
