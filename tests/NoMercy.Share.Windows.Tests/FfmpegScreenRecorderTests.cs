using System.Diagnostics;
using System.Text.Json;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

/// <summary>A desktop fact that also skips when there is no ffmpeg and ffprobe, from the path or NOMERCY_SHARE_FFMPEG (the ffmpeg file).</summary>
public sealed class RecordingFactAttribute : FactAttribute
{
    public RecordingFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive || Desktop.VirtualScreen().Width <= 0)
        {
            Skip = "No interactive Windows desktop on this machine.";
        }
        else if (FfmpegTools.Ffmpeg() is null || FfmpegTools.Ffprobe() is null)
        {
            Skip = "ffmpeg and ffprobe were not found.";
        }
    }
}

internal static class FfmpegTools
{
    public static string? Ffmpeg() =>
        FfmpegLocator.Find(Environment.GetEnvironmentVariable("NOMERCY_SHARE_FFMPEG") ?? string.Empty, Environment.GetEnvironmentVariable("PATH"));

    /// <summary>ffprobe lives next to ffmpeg in every build that ships it.</summary>
    public static string? Ffprobe() =>
        Ffmpeg() is { } ffmpeg && Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe") is { } probe && File.Exists(probe) ? probe : null;
}

public sealed class FfmpegScreenRecorderTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-recording-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [RecordingFact]
    public async Task TwoSecondsOfARegionBecomeAnH264Mp4UnderTheSizeCapAndTheRawFileIsGone()
    {
        ScreenRect screen = Desktop.VirtualScreen();
        CaptureSettings capture = new() { Folder = _folder };
        RecordingSettings recording = new() { FfmpegPath = FfmpegTools.Ffmpeg()! };
        FfmpegScreenRecorder recorder = new(
            new CaptureFiles(capture, "unused", () => DateTimeOffset.UtcNow), new FixedRegion(new ScreenRect(screen.X + 10, screen.Y + 20, 320, 240)), recording);

        await recorder.StartAsync(new RecordingOptions());
        Assert.True(recorder.IsRecording);
        await Task.Delay(TimeSpan.FromSeconds(2));
        CaptureResult result = await recorder.StopAsync();

        Assert.False(recorder.IsRecording);
        Assert.Equal("video/mp4", result.ContentType);
        Assert.EndsWith(".mp4", result.FilePath, StringComparison.Ordinal);
        Assert.Equal([result.FilePath], Directory.GetFiles(_folder));
        Assert.True(new FileInfo(result.FilePath).Length < recording.MaxSizeMegabytes * 1024 * 1024);

        using JsonDocument probe = JsonDocument.Parse(Probe(result.FilePath));
        JsonElement video = probe.RootElement.GetProperty("streams")[0];
        Assert.Equal("h264", video.GetProperty("codec_name").GetString());
        Assert.Equal((320, 240), (video.GetProperty("width").GetInt32(), video.GetProperty("height").GetInt32()));
        double seconds = double.Parse(probe.RootElement.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(seconds, 1.5, 3);
    }

    [RecordingFact]
    public async Task ACancelledRegionStartsNothing()
    {
        CaptureSettings capture = new() { Folder = _folder };
        FfmpegScreenRecorder recorder = new(
            new CaptureFiles(capture, "unused", () => DateTimeOffset.UtcNow), new FixedRegion(null), new RecordingSettings { FfmpegPath = FfmpegTools.Ffmpeg()! });

        await Assert.ThrowsAsync<OperationCanceledException>(() => recorder.StartAsync(new RecordingOptions()));

        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public async Task WithNoFfmpegTheStartSaysSoPlainly()
    {
        CaptureSettings capture = new() { Folder = _folder };
        FfmpegScreenRecorder recorder = new(
            new CaptureFiles(capture, "unused", () => DateTimeOffset.UtcNow), new FixedRegion(new ScreenRect(0, 0, 10, 10)),
            new RecordingSettings { FfmpegPath = Path.Combine(_folder, "no-such-ffmpeg") });

        RecordingException error = await Assert.ThrowsAsync<RecordingException>(() => recorder.StartAsync(new RecordingOptions()));

        Assert.Contains("Recording.FfmpegPath", error.Message, StringComparison.Ordinal);
        Assert.False(recorder.IsRecording);
    }

    private static string Probe(string file)
    {
        ProcessStartInfo start = new(FfmpegTools.Ffprobe()!) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height:format=duration", "-of", "json", file })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return output;
    }

    private sealed class FixedRegion(ScreenRect? chosen) : IRegionSelector
    {
        public Task<ScreenRect?> SelectAsync(ScreenFrame frozen, CancellationToken cancellationToken = default) => Task.FromResult(chosen);
    }
}
