using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

/// <summary>The recording page: the largest picture size, the codec, the encoder that works on this PC, and the speed.</summary>
public sealed class SettingsViewModelRecordingTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-settings-recording-{Guid.NewGuid():N}");
    private readonly FakeCredentialStore _credentials = new();
    private readonly SettingsFile _file;
    private readonly LiveSettings _live;
    private readonly HotkeyBinder _binder;

    public SettingsViewModelRecordingTests()
    {
        _file = new SettingsFile(SettingsFile.DefaultPath(_folder));
        _live = new LiveSettings(_file.Load());
        BarViewModel bar = new(
            new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
            new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()));
        _binder = new HotkeyBinder(new FakeGlobalHotkeys(), bar, _live.Current.Hotkeys);
    }

    private SettingsViewModel Open(RecordingSettings recording, IEncoderProbe? probe = null)
    {
        // Discord with a stored webhook: the smallest sharing setup that saves, so only the recording fields are under test.
        _credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", "https://discord.com/api/webhooks/000/made-up-token");
        ShareSettings current = new() { Storage = new StorageSettings { Provider = "discord" }, Recording = recording };
        _file.Save(current);
        _live.Replace(current);
        return new SettingsViewModel(_file, _live, _credentials, _binder, ["r2", "s3", "discord"], probe);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheSizesRunFrom720pTo4kPlusTheOriginalSize()
    {
        SettingsViewModel model = Open(new RecordingSettings());

        Assert.Equal(["720p", "1080p", "1440p", "4K", "Original size"], model.ResolutionChoices.Select(choice => choice.Label));
        Assert.Equal("1080p", model.SelectedResolution.Label);
    }

    [Fact]
    public void Picking4kSavesItsWidthAndHeight()
    {
        SettingsViewModel model = Open(new RecordingSettings());

        model.SelectedResolution = model.ResolutionChoices.Single(choice => choice.Label == "4K");
        model.Save();

        RecordingSettings saved = _file.Load().Recording;
        Assert.Equal((3840, 2160), (saved.MaxWidth, saved.MaxHeight));
        Assert.Equal((3840, 2160), (_live.Current.Recording.MaxWidth, _live.Current.Recording.MaxHeight));
    }

    [Fact]
    public void ASizeSetByHandInTheFileIsKeptAsItsOwnChoice()
    {
        SettingsViewModel model = Open(new RecordingSettings { MaxWidth = 2560, MaxHeight = 1080 });

        Assert.Equal("2560 × 1080", model.SelectedResolution.Label);
        model.Save();
        Assert.Equal((2560, 1080), (_file.Load().Recording.MaxWidth, _file.Load().Recording.MaxHeight));
    }

    [Fact]
    public void TheCodecAndSpeedAreSaved()
    {
        SettingsViewModel model = Open(new RecordingSettings());

        model.SelectedCodec = model.CodecChoices.Single(choice => choice.Value == "hevc");
        model.SelectedSpeed = model.SpeedChoices.Single(choice => choice.Value == "quality");
        model.Save();

        RecordingSettings saved = _file.Load().Recording;
        Assert.Equal(("hevc", "quality"), (saved.Codec, saved.Speed));
    }

    [Fact]
    public async Task OnlyTheEncodersThatWorkOnThisPcAreOffered()
    {
        FakeEncoderProbe probe = new() { Working = { ["h264"] = ["cpu", "nvidia"], ["av1"] = ["cpu"] } };
        SettingsViewModel model = Open(new RecordingSettings(), probe);
        await model.EncodersChecked;

        Assert.Equal(["cpu", "nvidia"], model.EncoderChoices.Select(choice => choice.Value));
        model.SelectedEncoder = model.EncoderChoices.Single(choice => choice.Value == "nvidia");

        model.SelectedCodec = model.CodecChoices.Single(choice => choice.Value == "av1");
        await model.EncodersChecked;

        Assert.Equal(["cpu"], model.EncoderChoices.Select(choice => choice.Value));
        Assert.Equal("cpu", model.SelectedEncoder.Value);
    }

    [Fact]
    public async Task ASavedGpuThatNoLongerWorksIsSaidAndTheCpuIsPicked()
    {
        FakeEncoderProbe probe = new() { Working = { ["h264"] = ["cpu"] } };
        SettingsViewModel model = Open(new RecordingSettings { Encoder = "nvidia" }, probe);
        await model.EncodersChecked;

        Assert.Equal("cpu", model.SelectedEncoder.Value);
        Assert.Equal("NVIDIA did not pass the test on this PC, so the CPU is used.", model.EncoderNote);
    }

    [Fact]
    public void WithoutAProbeOnlyTheCpuIsOffered()
    {
        SettingsViewModel model = Open(new RecordingSettings());

        Assert.Equal(["cpu"], model.EncoderChoices.Select(choice => choice.Value));
    }

    private sealed class FakeEncoderProbe : IEncoderProbe
    {
        public Dictionary<string, string[]> Working { get; } = [];

        public Task<IReadOnlyList<string>> WorkingFamiliesAsync(string codec, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Working.GetValueOrDefault(codec, ["cpu"]));
    }
}
