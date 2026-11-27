using System.Text.Json;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class RecordingSettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-recording-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheDefaultsAreTheAgreedValues()
    {
        RecordingSettings recording = new ShareSettings().Recording;

        Assert.Equal(string.Empty, recording.FfmpegPath);
        Assert.Equal(50, recording.MaxSizeMegabytes);
        Assert.Equal(0.05, recording.TargetBitsPerPixel);
        Assert.Equal(30, recording.FrameRate);
        Assert.Equal((1920, 1080), (recording.MaxWidth, recording.MaxHeight));
        Assert.Equal(128, recording.AudioBitrateKbps);
    }

    [Fact]
    public void ARecordingSettingInTheFileIsRead()
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { Recording = new { FfmpegPath = "/opt/ffmpeg", MaxSizeMegabytes = 8 } }));

        RecordingSettings recording = new SettingsFile(path).Load().Recording;

        Assert.Equal("/opt/ffmpeg", recording.FfmpegPath);
        Assert.Equal(8, recording.MaxSizeMegabytes);
        Assert.Equal(30, recording.FrameRate);
    }

    [Fact]
    public void SoundIsOffByDefault()
    {
        AudioSettings audio = new ShareSettings().Recording.Audio;

        Assert.False(audio.SystemSound);
        Assert.False(audio.Microphone);
    }

    [Fact]
    public void ASettingsFileFromBeforeSoundLoadsWithBothSwitchesOff()
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { Recording = new { FrameRate = 24 } }));

        RecordingSettings recording = new SettingsFile(path).Load().Recording;

        Assert.Equal(24, recording.FrameRate);
        Assert.Equal(new AudioSettings(), recording.Audio);
    }

    [Fact]
    public void TheSoundSwitchesInTheFileAreRead()
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { Recording = new { Audio = new { SystemSound = true, Microphone = true } } }));

        AudioSettings audio = new SettingsFile(path).Load().Recording.Audio;

        Assert.True(audio.SystemSound);
        Assert.True(audio.Microphone);
    }

    [Fact]
    public void ARecordingFileNameCarriesItsOwnExtension()
    {
        Assert.Equal("nomercy-share-20261127T153045123Z.mkv",
            CaptureFiles.FileName(ShareDefaults.CaptureFileNamePattern, new DateTimeOffset(2026, 11, 27, 15, 30, 45, 123, TimeSpan.Zero), ".mkv"));
    }

    [Fact]
    public void FfmpegIsTakenFromTheSettingWhenItExists()
    {
        Directory.CreateDirectory(_folder);
        string configured = Path.Combine(_folder, "my-ffmpeg");
        File.WriteAllText(configured, "x");

        Assert.Equal(configured, FfmpegLocator.Find(configured, pathVariable: null));
    }

    [Fact]
    public void ASettingThatPointsNowhereFindsNothingInsteadOfSilentlyUsingAnotherFfmpeg()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, FfmpegLocator.ExecutableName), "x");

        Assert.Null(FfmpegLocator.Find(Path.Combine(_folder, "missing"), pathVariable: _folder));
    }

    [Fact]
    public void WithNoSettingFfmpegIsFoundOnThePath()
    {
        Directory.CreateDirectory(_folder);
        string found = Path.Combine(_folder, FfmpegLocator.ExecutableName);
        File.WriteAllText(found, "x");

        string path = string.Join(Path.PathSeparator, Path.Combine(_folder, "nothing-here"), _folder);

        Assert.Equal(found, FfmpegLocator.Find("", path));
    }

    [Fact]
    public void WithNoSettingAndNothingOnThePathFfmpegIsNotFound()
    {
        Directory.CreateDirectory(_folder);

        Assert.Null(FfmpegLocator.Find("  ", _folder));
        Assert.Null(FfmpegLocator.Find("", null));
    }
}
