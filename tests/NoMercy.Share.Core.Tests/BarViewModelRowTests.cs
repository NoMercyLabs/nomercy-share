using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

/// <summary>The bar's modes: picking one captures nothing, what a target does in each, and what Start records.</summary>
public sealed class BarViewModelRowTests : IDisposable
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

    private BarViewModel Bar(RecordingSoundStore? sound = null, LastAction? last = null)
    {
        LastActionStore store = new(File());
        if (last is not null)
        {
            store.Save(last);
        }

        return new BarViewModel(
            new CaptureFlow(_screen, _host, new CaptureSettings { HideDelayMilliseconds = 0 }),
            new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
            recorder: _recorder,
            web: new FakeWebPageCapture(),
            sound: sound,
            lastAction: store);
    }

    private static (bool, bool, bool) Picked(BarViewModel bar) => (bar.Row.IsAreaPicked, bar.Row.IsWindowPicked, bar.Row.IsFullScreenPicked);

    [Theory]
    [InlineData(BarAction.Screenshot, BarMode.Screenshot)]
    [InlineData(BarAction.Record, BarMode.Record)]
    public async Task PickingAModeOnlySelectsItAndASecondPressKeepsIt(BarAction button, BarMode mode)
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(button);
        await bar.RunAsync(button);

        Assert.Equal(mode, bar.Row.Mode);
        Assert.Empty(_screen.Calls);
        Assert.False(_recorder.IsRecording);
    }

    [Theory]
    [InlineData(CaptureKind.Region, "region")]
    [InlineData(CaptureKind.Window, "window")]
    [InlineData(CaptureKind.FullScreen, "fullscreen")]
    public async Task ATargetInScreenshotModeCapturesAtOnceAndNoTargetLooksPicked(CaptureKind target, string call)
    {
        BarViewModel bar = Bar();
        Assert.Equal((false, false, false), Picked(bar));

        await bar.PickTargetAsync(target);

        Assert.Equal([call], _screen.Calls);
        Assert.Equal((false, false, false), Picked(bar));
    }

    [Fact]
    public async Task ATargetInRecordModeIsOnlyPickedAndOneAtATime()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);
        Assert.Equal((true, false, false), Picked(bar));

        bar.PickTargetCommand.Execute(CaptureKind.FullScreen);

        Assert.Equal((false, false, true), Picked(bar));
        Assert.Empty(_screen.Calls);
        Assert.False(_recorder.IsRecording);
    }

    [Fact]
    public async Task TheRecordHotkeyShowsTheBarInRecordModeAndRecordsNothing()
    {
        BarViewModel bar = Bar();
        int shows = 0;
        bar.ShowRequested += (_, _) => shows++;

        await bar.RunAsync(BarAction.Record);

        Assert.True(bar.Row.IsRecordMode);
        Assert.False(_recorder.IsRecording);
        Assert.Equal(1, shows);
    }

    [Fact]
    public async Task TheGlobePicksWebModeAndOpensTheWebPageCaptureAtOnce()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.WebPage);

        Assert.True(bar.Row.IsWebMode);
        Assert.False(bar.Row.ShowsTargets);
    }

    [Theory]
    [InlineData(null, BarMode.Screenshot)]
    [InlineData(LastActionTask.Screenshot, BarMode.Screenshot)]
    [InlineData(LastActionTask.Recording, BarMode.Record)]
    [InlineData(LastActionTask.WebPage, BarMode.Web)]
    public async Task TheBarOpensInTheModeOfTheLastActionAndReturnsToItWhenDismissed(LastActionTask? task, BarMode mode)
    {
        BarViewModel bar = Bar(last: task is { } saved ? new LastAction(saved, CaptureKind.Window) : null);
        Assert.Equal(mode, bar.Row.Mode);

        await bar.RunAsync(mode == BarMode.Record ? BarAction.Screenshot : BarAction.Record);
        bar.ReturnToLastMode();

        Assert.Equal(mode, bar.Row.Mode);
    }

    [Fact]
    public async Task TheModeFollowsWhatWasStartedByAHotkey()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);

        await bar.RunAsync(BarAction.FullScreen);

        Assert.Equal(BarMode.Screenshot, bar.Row.Mode);
    }

    [Fact]
    public void EscapeWithNothingWaitingAsksToHideTheBar()
    {
        BarViewModel bar = Bar();
        int closes = 0;
        bar.CloseRequested += (_, _) => closes++;

        bar.Escape();

        Assert.Equal(1, closes);
    }

    [Theory]
    [InlineData(CaptureKind.Region, false)]
    [InlineData(CaptureKind.Window, true)]
    [InlineData(CaptureKind.FullScreen, false)]
    public async Task StartRecordsTheSelectedTargetAndOnlyAWindowNeedsTheBarOutOfTheWay(CaptureKind source, bool hides)
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Record);
        bar.PickTargetCommand.Execute(source);
        bool? hiddenAtStart = null;
        _recorder.OnStart = () => hiddenAtStart = _host.Hidden;

        await bar.StartRecordingAsync();

        Assert.True(bar.IsRecording);
        Assert.False(bar.Row.ShowsRecordControls);
        Assert.False(bar.Row.ShowsTargets);
        Assert.Equal(source, _recorder.Options?.Source);
        Assert.Equal(hides, hiddenAtStart);
        Assert.False(_host.Hidden);
    }

    [Fact]
    public async Task WhileRecordingTheRecordButtonStopsAtOnce()
    {
        BarViewModel bar = Bar();
        await bar.OpenAndStartAsync();

        await bar.RunAsync(BarAction.Record);

        Assert.False(bar.IsRecording);
        Assert.True(bar.Row.ShowsRecordControls);
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
    /// <summary>The two steps a user takes: the video button picks record mode, Start records.</summary>
    public static async Task OpenAndStartAsync(this BarViewModel bar)
    {
        await bar.RunAsync(BarAction.Record);
        await bar.StartRecordingAsync();
    }
}
