using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

/// <summary>The bar hotkey starts the last used action at once, and another choice replaces an open area picker.</summary>
public sealed class BarViewModelLastActionTests : IDisposable
{
    private readonly FakeRegionSelector _overlay = new();
    private readonly AreaPicker _picker;
    private readonly FakeScreenCapture _screen = new();
    private readonly FakeScreenRecorder _recorder = new();
    private readonly FakeWebPageCapture _web = new();
    private readonly FakeCaptureHost _host = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-last-{Guid.NewGuid():N}");

    public BarViewModelLastActionTests()
    {
        _picker = new AreaPicker(_overlay);
        _screen.Picker = _picker;
        _recorder.Picker = _picker;
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private LastActionStore Store() => new(new SettingsFile(Path.Combine(_folder, "settings.json")));

    private BarViewModel Bar() => new(
        new CaptureFlow(_screen, _host, new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
        recorder: _recorder,
        web: _web,
        lastAction: Store(),
        picker: _picker);

    private static LastAction Shot(CaptureKind source) => new(LastActionTask.Screenshot, source);

    private static LastAction Recording(CaptureKind source) => new(LastActionTask.Recording, source);

    private static Task Done(Task run) => run.WaitAsync(TimeSpan.FromSeconds(10));

    [Fact]
    public async Task WithNothingSavedTheHotkeyShowsTheBarAndPicksAScreenshotArea()
    {
        BarViewModel bar = Bar();
        int shows = 0;
        bar.ShowRequested += (_, _) => shows++;

        Task run = bar.RunAsync(BarAction.Show);
        await _overlay.WaitUntilOpenAsync();
        _overlay.Choose();
        await Done(run);

        Assert.Equal(1, shows);
        Assert.Equal(["region"], _screen.Calls);
        Assert.NotNull(bar.Choice);
    }

    [Theory]
    [InlineData(BarAction.Window, CaptureKind.Window, "window")]
    [InlineData(BarAction.FullScreen, CaptureKind.FullScreen, "fullscreen")]
    public async Task AStartedScreenshotIsSavedAndTheHotkeyRepeatsItAtOnce(BarAction started, CaptureKind source, string call)
    {
        await Done(Bar().RunAsync(started));
        Assert.Equal(Shot(source), Store().Load());
        BarViewModel restarted = Bar();

        await Done(restarted.RunAsync(BarAction.Show));

        Assert.Equal([call, call], _screen.Calls);
        Assert.NotNull(restarted.Choice);
    }

    [Fact]
    public async Task TheHotkeyRecordsAnAreaAfterItIsChosen()
    {
        Store().Save(Recording(CaptureKind.Region));
        BarViewModel bar = Bar();

        Task run = bar.RunAsync(BarAction.Show);
        await _overlay.WaitUntilOpenAsync();
        Assert.False(_recorder.IsRecording);
        _overlay.Choose();
        await Done(run);

        Assert.True(_recorder.IsRecording);
        Assert.Equal(CaptureKind.Region, _recorder.Options?.Source);
    }

    [Theory]
    [InlineData(CaptureKind.Window)]
    [InlineData(CaptureKind.FullScreen)]
    public async Task TheHotkeyOnlyPreselectsARecordingThatHasNoPicker(CaptureKind source)
    {
        Store().Save(Recording(source));
        BarViewModel bar = Bar();

        await Done(bar.RunAsync(BarAction.Show));

        Assert.True(bar.Row.IsRecordMode);
        Assert.False(_recorder.IsRecording);
        Assert.Equal(source == CaptureKind.Window, bar.Row.IsWindowPicked);
        Assert.Equal(source == CaptureKind.FullScreen, bar.Row.IsFullScreenPicked);
    }

    [Fact]
    public async Task TheHotkeyOpensTheWebPageCaptureWhenThatWasLast()
    {
        await Done(Bar().CaptureWebPageAsync());

        await Done(Bar().CaptureWebPageAsync(BarAction.Show));

        Assert.Equal(2, _web.Requests.Count);
        Assert.Equal(LastActionTask.WebPage, Store().Load().Task);
    }

    [Fact]
    public async Task AStartedRecordingIsSavedButPickingItsModeAndTargetIsNot()
    {
        BarViewModel bar = Bar();

        await Done(bar.RunAsync(BarAction.Record));
        bar.PickTargetCommand.Execute(CaptureKind.FullScreen);
        Assert.Equal(new LastAction(), Store().Load());
        await Done(bar.StartRecordingAsync());

        Assert.Equal(Recording(CaptureKind.FullScreen), Store().Load());
    }

    [Fact]
    public async Task WhileRecordingTheHotkeyOnlyShowsTheBar()
    {
        BarViewModel bar = Bar();
        await Done(bar.RunAsync(BarAction.Record));
        bar.PickTargetCommand.Execute(CaptureKind.FullScreen);
        await Done(bar.StartRecordingAsync());
        int shows = 0;
        bar.ShowRequested += (_, _) => shows++;

        await Done(bar.RunAsync(BarAction.Show));

        Assert.Equal(1, shows);
        Assert.True(_recorder.IsRecording);
        Assert.False(bar.Row.ShowsRecordControls);
        Assert.Empty(_screen.Calls);
    }

    [Fact]
    public async Task ASecondHotkeyPressStartsNoSecondPicker()
    {
        BarViewModel bar = Bar();
        Task run = bar.RunAsync(BarAction.Show);
        await _overlay.WaitUntilOpenAsync();

        await Done(bar.RunAsync(BarAction.Show));

        Assert.Equal(1, _overlay.Opens);
        Assert.False(run.IsCompleted);
        _overlay.Escape();
        await Done(run);
    }

    [Fact]
    public async Task EscapeKeepsTheSavedActionAndSaysNothing()
    {
        Store().Save(Recording(CaptureKind.Region));
        BarViewModel bar = Bar();
        Task run = bar.RunAsync(BarAction.Show);
        await _overlay.WaitUntilOpenAsync();

        _overlay.Escape();
        await Done(run);

        Assert.Equal(Recording(CaptureKind.Region), Store().Load());
        Assert.False(_recorder.IsRecording);
        Assert.Null(bar.Notice);
        Assert.Null(bar.Choice);
    }

    [Fact]
    public async Task AnotherActionCancelsTheOpenPickerStartsAndIsSaved()
    {
        BarViewModel bar = Bar();
        Task open = bar.RunAsync(BarAction.Show);
        await _overlay.WaitUntilOpenAsync();

        await Done(bar.RunAsync(BarAction.FullScreen));
        await Done(open);

        Assert.Equal(["region", "fullscreen"], _screen.Calls);
        Assert.EndsWith("fullscreen.png", bar.Choice?.Capture.FilePath);
        Assert.Equal(Shot(CaptureKind.FullScreen), Store().Load());
    }

    [Theory]
    [InlineData(BarAction.Screenshot, BarMode.Screenshot)]
    [InlineData(BarAction.Record, BarMode.Record)]
    public async Task PickingAModeCancelsTheOpenPicker(BarAction button, BarMode mode)
    {
        BarViewModel bar = Bar();
        Task open = bar.RunAsync(BarAction.Show);
        await _overlay.WaitUntilOpenAsync();

        await Done(bar.RunAsync(button));
        await Done(open);

        Assert.Equal(mode, bar.Row.Mode);
        Assert.Null(bar.Choice);
        Assert.Equal(new LastAction(), Store().Load());
    }

    [Fact]
    public async Task EscapeOnTheBarCancelsAnOpenPickerAndKeepsTheBar()
    {
        BarViewModel bar = Bar();
        int closes = 0;
        bar.CloseRequested += (_, _) => closes++;
        Task open = bar.RunAsync(BarAction.Show);
        await _overlay.WaitUntilOpenAsync();

        bar.Escape();
        await Done(open);

        Assert.Equal(0, closes);
        Assert.Null(bar.Choice);
    }

    [Fact]
    public async Task ADedicatedHotkeyReplacesAnOpenPickerWithOneNewPicker()
    {
        Store().Save(Recording(CaptureKind.Region));
        BarViewModel bar = Bar();
        Task recording = bar.RunAsync(BarAction.Show);
        await _overlay.WaitUntilOpenAsync();

        Task shot = bar.RunAsync(BarAction.Region);
        await _overlay.WaitUntilOpenAsync();
        await Done(recording);
        _overlay.Choose();
        await Done(shot);

        Assert.False(_recorder.IsRecording);
        Assert.Equal(["region"], _screen.Calls);
        Assert.Equal(Shot(CaptureKind.Region), Store().Load());
    }

    [Fact]
    public void TheLastActionIsWrittenAsWordsAndOldSettingsFilesStillLoad()
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, """{"Bar":{"NoticeSeconds":7}}""");
        Assert.Equal(Shot(CaptureKind.Region), Store().Load());

        Store().Save(Recording(CaptureKind.Window));

        Assert.Equal(Recording(CaptureKind.Window), Store().Load());
        Assert.Equal(7, new SettingsFile(path).Load().Bar.NoticeSeconds);
        string written = File.ReadAllText(path);
        Assert.Contains("\"Recording\"", written);
        Assert.Contains("\"Window\"", written);
    }
}
