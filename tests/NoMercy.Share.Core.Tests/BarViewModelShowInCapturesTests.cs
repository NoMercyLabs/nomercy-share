using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

/// <summary>The setting that shows the bar in captures: screenshots and recordings alike.</summary>
public sealed class BarViewModelShowInCapturesTests : IDisposable
{
    private readonly FakeScreenCapture _screen = new();
    private readonly FakeScreenRecorder _recorder = new();
    private readonly FakeCaptureHost _host = new();
    private readonly LiveSettings _live = new(new ShareSettings());
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-shown-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private BarViewModel Bar() => new(
        new CaptureFlow(_screen, _host, new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
        recorder: _recorder,
        live: _live);

    private void ShowTheBar(bool show) =>
        _live.Replace(new ShareSettings { Bar = new BarSettings { ShowInCaptures = show } });

    private ShareSettings Loaded(string json)
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, json);
        return new SettingsFile(path).Load();
    }

    [Fact]
    public async Task TheSwitchIsOffAtFirstAndCapturesLeaveTheBarOut()
    {
        BarViewModel bar = Bar();
        Assert.False(new ShareSettings().Bar.ShowInCaptures);

        await bar.OpenAndStartAsync();

        Assert.True(bar.IsRecording);
        Assert.True(bar.IsBarExcludedFromCaptures);
    }

    [Fact]
    public void TheSwitchLetsTheBarInAtOnceAndTellsTheWindow()
    {
        BarViewModel bar = Bar();
        List<bool> told = [];
        bar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BarViewModel.IsBarExcludedFromCaptures))
            {
                told.Add(bar.IsBarExcludedFromCaptures);
            }
        };

        ShowTheBar(true);
        ShowTheBar(false);

        Assert.Equal([false, true], told);
    }

    [Theory]
    [InlineData(BarAction.Region)]
    [InlineData(BarAction.FullScreen)]
    public async Task WithTheSwitchOnAnAreaOrFullScreenShotKeepsTheBarOnScreen(BarAction shot)
    {
        BarViewModel bar = Bar();
        ShowTheBar(true);

        await bar.RunAsync(shot);

        Assert.Empty(_host.Events);
        Assert.False(bar.IsBarExcludedFromCaptures);
    }

    [Fact]
    public async Task AWindowShotStillHidesTheBarSoThePreviousWindowIsTheActiveOne()
    {
        BarViewModel bar = Bar();
        ShowTheBar(true);

        await bar.RunAsync(BarAction.Window);

        Assert.Equal(["hide", "show"], _host.Events);
    }

    [Theory]
    [InlineData(BarAction.Region)]
    [InlineData(BarAction.Window)]
    [InlineData(BarAction.FullScreen)]
    public async Task WithTheSwitchOffEveryShotHidesTheBar(BarAction shot)
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(shot);

        Assert.Equal(["hide", "show"], _host.Events);
    }

    [Theory]
    [InlineData("""{ "Recording": { "ShowTool": true } }""", true)]
    [InlineData("""{ "Recording": { "ShowTool": false } }""", false)]
    [InlineData("""{ "Recording": { "FrameRate": 24 } }""", false)]
    [InlineData("""{ "Bar": { "ShowInCaptures": false }, "Recording": { "ShowTool": true } }""", false)]
    [InlineData("""{ "Bar": { "NoticeSeconds": 9 }, "Recording": { "ShowTool": true } }""", true)]
    public void TheOldVideoSwitchCarriesOverUntilTheNewOneIsSaved(string json, bool shown) =>
        Assert.Equal(shown, Loaded(json).Bar.ShowInCaptures);
}
