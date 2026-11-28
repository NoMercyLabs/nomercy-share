using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

/// <summary>The setting that shows the tool in a recording: when the bar may be seen by a capture.</summary>
public sealed class BarViewModelToolInRecordingTests : IDisposable
{
    private readonly FakeScreenCapture _screen = new();
    private readonly FakeScreenRecorder _recorder = new();
    private readonly LiveSettings _live = new(new ShareSettings());
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-tool-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private SettingsFile SettingsOnDisk() => new(Path.Combine(_folder, "settings.json"));

    private BarViewModel Bar() => new(
        new CaptureFlow(_screen, new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
        recorder: _recorder,
        live: _live);

    private void ShowTheTool(bool show) =>
        _live.Replace(new ShareSettings { Recording = new RecordingSettings { ShowTool = show } });

    private async Task<BarViewModel> RecordingWithTheSwitchOnAsync()
    {
        BarViewModel bar = Bar();
        ShowTheTool(true);
        await bar.OpenAndStartAsync();
        return bar;
    }

    /// <summary>The last value a listener such as the bar window was told, or null when it was told nothing.</summary>
    private static Func<bool?> Told(BarViewModel bar)
    {
        bool? told = null;
        bar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BarViewModel.IsBarExcludedFromCaptures))
            {
                told = bar.IsBarExcludedFromCaptures;
            }
        };
        return () => told;
    }

    [Fact]
    public async Task TheSwitchIsOffAtFirstAndARecordingLeavesTheBarOut()
    {
        BarViewModel bar = Bar();
        Assert.False(new ShareSettings().Recording.ShowTool);

        await bar.OpenAndStartAsync();

        Assert.True(bar.IsRecording);
        Assert.True(bar.IsBarExcludedFromCaptures);
    }

    [Fact]
    public void ASettingsFileWithoutTheKeyKeepsTheToolHidden()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "settings.json"), """{ "Recording": { "FrameRate": 24 } }""");

        Assert.False(SettingsOnDisk().Load().Recording.ShowTool);
    }

    [Fact]
    public async Task ASaveDuringARecordingTakesEffectAtOnce()
    {
        BarViewModel bar = Bar();
        await bar.OpenAndStartAsync();
        Func<bool?> told = Told(bar);

        ShowTheTool(true);
        Assert.False(told());

        ShowTheTool(false);
        Assert.True(told());
    }

    [Fact]
    public void WithTheSwitchOnAnIdleBarStaysOutOfCaptures()
    {
        BarViewModel bar = Bar();

        ShowTheTool(true);

        Assert.True(bar.IsBarExcludedFromCaptures);
    }

    [Fact]
    public async Task OnlyARunningRecordingLetsTheBarInAndAStopTakesItOutAgain()
    {
        BarViewModel bar = Bar();
        ShowTheTool(true);
        Func<bool?> told = Told(bar);

        await bar.OpenAndStartAsync();
        Assert.False(told());

        await bar.RunAsync(BarAction.Record);

        Assert.True(told());
        Assert.True(bar.IsBarExcludedFromCaptures);
    }

    [Fact]
    public async Task ARecordingThatFailsTakesTheBarOutAgain()
    {
        BarViewModel bar = await RecordingWithTheSwitchOnAsync();
        Assert.False(bar.IsBarExcludedFromCaptures);
        Func<bool?> told = Told(bar);

        _recorder.Fail("gone");

        Assert.True(told());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AStopThatFailsOrIsCancelledTakesTheBarOutAgain(bool cancelled)
    {
        BarViewModel bar = await RecordingWithTheSwitchOnAsync();
        _recorder.StopThrows = cancelled ? new OperationCanceledException() : new RecordingException("broken");
        Func<bool?> told = Told(bar);

        await bar.RunAsync(BarAction.Record);

        Assert.True(told());
    }

    [Fact]
    public async Task ACancelledStartNeverLetsTheBarIn()
    {
        BarViewModel bar = Bar();
        ShowTheTool(true);
        _recorder.StartThrows = new OperationCanceledException();

        await bar.OpenAndStartAsync();

        Assert.True(bar.IsBarExcludedFromCaptures);
    }

    [Theory]
    [InlineData(BarAction.Region)]
    [InlineData(BarAction.Window)]
    [InlineData(BarAction.FullScreen)]
    public async Task AScreenshotDuringARecordingNeverHasTheBar(BarAction shot)
    {
        BarViewModel bar = await RecordingWithTheSwitchOnAsync();
        Func<bool?> told = Told(bar);
        bool? toldBeforeTheShot = null;
        _screen.OnCapture = () => toldBeforeTheShot = told();

        await bar.RunAsync(shot);

        Assert.True(toldBeforeTheShot);
        Assert.False(told());
        Assert.False(bar.IsBarExcludedFromCaptures);
    }
}
