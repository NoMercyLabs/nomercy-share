using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

/// <summary>The Screenshot and Record menus under the bar: what opens, what closes them, and what Start records.</summary>
public sealed class BarViewModelMenuTests : IDisposable
{
    private readonly FakeScreenCapture _screen = new();
    private readonly FakeScreenRecorder _recorder = new();
    private readonly FakeCaptureHost _host = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-menu-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private SettingsFile File() => new(Path.Combine(_folder, "settings.json"));

    private BarViewModel Bar(RecordingSoundStore? sound = null) => new(
        new CaptureFlow(_screen, _host, new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
        recorder: _recorder,
        sound: sound);

    [Fact]
    public async Task ScreenshotOpensItsMenuAndASecondPressClosesIt()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Screenshot);
        Assert.True(bar.IsScreenshotMenuOpen);
        Assert.Empty(_screen.Calls);

        await bar.RunAsync(BarAction.Screenshot);
        Assert.False(bar.IsScreenshotMenuOpen);
        Assert.Equal(BarMenu.None, bar.Menu);
    }

    [Theory]
    [InlineData(BarAction.Region, "region")]
    [InlineData(BarAction.Window, "window")]
    [InlineData(BarAction.FullScreen, "fullscreen")]
    public async Task PickingAShotTakesItAtOnceWithTheMenuAlreadyClosed(BarAction pick, string call)
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Screenshot);
        Assert.Equal(BarMenu.Screenshot, bar.Menu);
        BarMenu? during = null;
        _screen.OnCapture = () => during = bar.Menu;

        await bar.RunAsync(pick);

        Assert.Equal([call], _screen.Calls);
        Assert.Equal(BarMenu.None, during);
        Assert.Equal(BarMenu.None, bar.Menu);
    }

    [Fact]
    public async Task RecordOpensItsMenuShowsTheBarAndRecordsNothing()
    {
        BarViewModel bar = Bar();
        int shows = 0;
        bar.ShowRequested += (_, _) => shows++;

        await bar.RunAsync(BarAction.Record);

        Assert.True(bar.IsRecordMenuOpen);
        Assert.False(_recorder.IsRecording);
        Assert.Equal(1, shows);

        await bar.RunAsync(BarAction.Record);
        Assert.False(bar.IsRecordMenuOpen);
        Assert.False(_recorder.IsRecording);
        Assert.Equal(1, shows);
    }

    [Fact]
    public async Task OpeningOneMenuClosesTheOther()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Screenshot);
        await bar.RunAsync(BarAction.Record);
        Assert.Equal(BarMenu.Record, bar.Menu);

        await bar.RunAsync(BarAction.Screenshot);
        Assert.Equal(BarMenu.Screenshot, bar.Menu);
    }

    [Theory]
    [InlineData(BarAction.Close)]
    [InlineData(BarAction.Settings)]
    [InlineData(BarAction.WebPage)]
    [InlineData(BarAction.Recent)]
    public async Task AnyOtherButtonClosesTheMenu(BarAction other)
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);
        Assert.Equal(BarMenu.Record, bar.Menu);

        await bar.RunAsync(other);

        Assert.Equal(BarMenu.None, bar.Menu);
    }

    [Fact]
    public async Task AreaIsSelectedByDefaultAndOneTargetIsSelectedAtATime()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);
        Assert.Equal((true, false, false), (bar.IsRecordArea, bar.IsRecordWindow, bar.IsRecordFullScreen));

        bar.SelectRecordSourceCommand.Execute(CaptureKind.FullScreen);

        Assert.Equal((false, false, true), (bar.IsRecordArea, bar.IsRecordWindow, bar.IsRecordFullScreen));
        Assert.False(_recorder.IsRecording);
    }

    [Theory]
    [InlineData(CaptureKind.Region, false)]
    [InlineData(CaptureKind.Window, true)]
    [InlineData(CaptureKind.FullScreen, false)]
    public async Task StartRecordsTheSelectedTargetAndOnlyAWindowNeedsTheBarOutOfTheWay(CaptureKind source, bool hides)
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);
        bar.SelectRecordSourceCommand.Execute(source);
        bool? hiddenAtStart = null;
        _recorder.OnStart = () => hiddenAtStart = _host.Hidden;

        await bar.StartRecordingAsync();

        Assert.True(bar.IsRecording);
        Assert.Equal(BarMenu.None, bar.Menu);
        Assert.Equal(source, _recorder.Options?.Source);
        Assert.Equal(hides, hiddenAtStart);
        Assert.False(_host.Hidden);
    }

    [Fact]
    public async Task WhileRecordingTheRecordButtonStopsAtOnceWithNoMenu()
    {
        BarViewModel bar = Bar();
        await bar.OpenAndStartAsync();

        await bar.RunAsync(BarAction.Record);

        Assert.False(bar.IsRecording);
        Assert.Equal(BarMenu.None, bar.Menu);
    }

    [Fact]
    public async Task TheSoundTogglesSaveTheSettingsPageSwitches()
    {
        LiveSettings live = new(new ShareSettings());
        BarViewModel bar = Bar(new RecordingSoundStore(File(), live));
        Assert.False(bar.IsMicrophoneOn);
        Assert.False(bar.IsDesktopAudioOn);

        bar.ToggleMicrophoneCommand.Execute(null);

        Assert.True(bar.IsMicrophoneOn);
        Assert.True(File().Load().Recording.Audio.Microphone);
        Assert.True(live.Current.Recording.Audio.Microphone);
        Assert.False(File().Load().Recording.Audio.SystemSound);

        bar.ToggleDesktopAudioCommand.Execute(null);
        bar.ToggleMicrophoneCommand.Execute(null);

        Assert.Equal((true, false), (File().Load().Recording.Audio.SystemSound, File().Load().Recording.Audio.Microphone));
        Assert.Equal((true, false), (bar.IsDesktopAudioOn, bar.IsMicrophoneOn));
    }

    [Fact]
    public async Task AnOpenSettingsPageAndTheBarShowTheSameSwitchesAndSaveKeepsTheBarsChoice()
    {
        LiveSettings live = new(new ShareSettings());
        BarViewModel bar = Bar(new RecordingSoundStore(File(), live));
        SettingsViewModel page = new(File(), live, new FakeCredentialStore(), new HotkeyBinder(new FakeGlobalHotkeys(), bar, new HotkeySettings()), ["r2"]);
        List<string?> changed = [];
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.ToggleMicrophoneCommand.Execute(null);
        Assert.True(page.RecordMicrophone);

        page.RecordSystemSound = true;
        page.Save();

        Assert.True(bar.IsDesktopAudioOn);
        Assert.True(bar.IsMicrophoneOn);
        Assert.Contains(nameof(BarViewModel.IsDesktopAudioOn), changed);
        Assert.True(File().Load().Recording.Audio.Microphone);
        await Task.CompletedTask;
    }
}

internal static class BarRecordingSteps
{
    /// <summary>The two steps a user takes: the Record button opens the menu, Start records.</summary>
    public static async Task OpenAndStartAsync(this BarViewModel bar)
    {
        await bar.RunAsync(BarAction.Record);
        await bar.StartRecordingAsync();
    }
}
