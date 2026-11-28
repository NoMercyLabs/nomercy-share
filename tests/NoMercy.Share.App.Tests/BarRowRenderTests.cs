using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoMercy.Share.App.Icons;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;
using NoMercy.Share.Core.Tests;

namespace NoMercy.Share.App.Tests;

/// <summary>The bar's one row: which buttons each mode shows, and that the grown row stays on the screen.</summary>
public sealed class BarRowRenderTests
{
    private const string RenderFolderVariable = "SHARE_RENDER_DIR";
    private const double MinimumHitSize = 36;

    private static readonly string[] Modes = ["Screenshot", "Record the screen", "Capture a web page"];
    private static readonly string[] Targets = ["Area", "Window", "Full screen"];
    private static readonly string[] Tail = ["Recent links", "Settings", "Close NoMercy Share"];

    [Fact]
    public void ScreenshotModeShowsTheModesAndTheTargets() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        Pump(bar.RunAsync(BarAction.Screenshot));

        AssertRow(window, [.. Modes, .. Targets, .. Tail], "row-screenshot.png");
        Assert.Equal(4, Separators(window));
    }));

    [Fact]
    public void RecordModeAddsTheSoundSwitchesAndStart() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        Pump(bar.RunAsync(BarAction.Record));

        AssertRow(window, [.. Modes, .. Targets, "Microphone", "Desktop audio", "Start", .. Tail], "row-record.png");
        Assert.Equal(5, Separators(window));
    }));

    [Fact]
    public void WebModeShowsNoTargets() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        _ = bar.RunAsync(BarAction.WebPage);

        AssertRow(window, [.. Modes, .. Tail], "row-web.png");
        Assert.Equal(3, Separators(window));
    }));

    [Fact]
    public void WhileRecordingTheRowHidesTargetsSoundAndStart() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        Pump(bar.RunAsync(BarAction.Record));
        Pump(bar.StartRecordingAsync());
        bar.ClearNotice();

        AssertRow(window, ["Screenshot", "Stop recording", "Capture a web page", .. Tail], "row-recording.png");
        Assert.Equal(3, Separators(window));
        Assert.True(Row(window).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("recording-time")).IsEffectivelyVisible);
    }));

    [Fact]
    public void NothingSitsBetweenCameraVideoAndGlobeAndThereIsNoSecondPill() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        Pump(bar.RunAsync(BarAction.Record));
        Control[] items = [.. ((Panel)Row(window).Child!).Children.Where(child => child.IsVisible)];
        int camera = Array.FindIndex(items, item => item.Name == "ScreenshotButton");

        Assert.Equal(["ScreenshotButton", "RecordButton", "WebButton"], items.Skip(camera).Take(3).Select(item => item.Name));
        Assert.Null(window.FindControl<Border>("ScreenshotMenu"));
        Assert.Null(window.FindControl<Border>("RecordMenu"));
    }));

    [Fact]
    public void ThePickedModeAndTargetSayTheyAreSelected() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        Pump(bar.RunAsync(BarAction.Record));

        Button[] picked = [.. Shown(window).Where(button => AutomationProperties.GetHelpText(button) == "Selected")];

        Assert.Equal(["Record the screen", "Area"], picked.Select(AutomationProperties.GetName));
        Assert.All(picked, button => Assert.Contains("active", button.Classes));
    }));

    [Fact]
    public void ASoundSwitchThatIsOffShowsACrossedOutIcon() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        Pump(bar.RunAsync(BarAction.Record));

        string[] icons = [.. Row(window).GetVisualDescendants().OfType<MoooomIcon>().Where(icon => icon.IsEffectivelyVisible).Select(icon => icon.Kind)];

        Assert.Equal(["grabDots", "camera", "webcam", "globe", "crossHair", "macOsWindow", "monitor", "mic", "volumeMuted", "link", "settings", "cross"], icons);
    }));

    [Fact]
    public void ARowThatGrowsAtTheRightEdgeStaysOnTheScreenAndGoesBackWhenItShrinks() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        _ = bar.RunAsync(BarAction.WebPage);
        Dispatcher.UIThread.RunJobs();
        PixelRect area = window.Screens.Primary!.WorkingArea;
        int narrow = Width(window);
        PixelPoint home = new(area.Right - narrow, area.Y + 100);
        window.Position = home;
        Dispatcher.UIThread.RunJobs();

        Pump(bar.RunAsync(BarAction.Record));
        Dispatcher.UIThread.RunJobs();

        Assert.True(Width(window) > narrow, $"the row did not grow past {narrow}");
        Assert.True(window.Position.X + Width(window) <= area.Right, $"the row ends at {window.Position.X + Width(window)}, the screen at {area.Right}");
        Assert.Equal(home.Y, window.Position.Y);

        _ = bar.RunAsync(BarAction.WebPage);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(home, window.Position);
    }));

    [Fact]
    public void EscapeOnTheBarHidesIt() => HeadlessApp.Run(() => WithBar((window, _) =>
    {
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.IsVisible);
    }));

    [Fact]
    public void TheBarKeepsItselfOutOfCapturesOnceItIsOpen() => HeadlessApp.Run(() =>
    {
        RecordingExclusion exclusion = new();
        WithBar((_, _) => Assert.Single(exclusion.Windows), exclusion);
    });

    private static int Width(Window window) => (int)Math.Ceiling(window.Bounds.Width * window.RenderScaling);

    private static Border Row(BarWindow window) => window.FindControl<Border>("Pill")!;

    private static IEnumerable<Button> Shown(BarWindow window) =>
        Row(window).GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible);

    private static int Separators(BarWindow window) =>
        Row(window).GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains("separator") && border.IsEffectivelyVisible);

    private static void AssertRow(BarWindow window, string[] names, string picture)
    {
        Dispatcher.UIThread.RunJobs();
        using Avalonia.Media.Imaging.WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable(RenderFolderVariable) is { Length: > 0 } folder)
        {
            frame.Save(Path.Combine(folder, picture));
            File.WriteAllText(Path.Combine(folder, Path.ChangeExtension(picture, ".width.txt")), $"{Row(window).Bounds.Width}");
        }

        Button[] buttons = [.. Shown(window)];
        Assert.Equal(names, buttons.Select(AutomationProperties.GetName));
        Assert.All(buttons, button => Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(button) as string)));
        Assert.All(buttons, button => Assert.True(button.Bounds.Height >= MinimumHitSize && button.Bounds.Width >= MinimumHitSize));
    }

    private static void Pump(Task task)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.True(task.IsCompletedSuccessfully, "the UI work did not finish");
    }

    private static void WithBar(Action<BarWindow, BarViewModel> test, ICaptureExclusion? exclusion = null)
    {
        string root = Path.Combine(Path.GetTempPath(), $"share-row-render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            BarViewModel bar = new(
                new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
                new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
                recorder: new FakeScreenRecorder(),
                web: new FakeWebPageCapture(),
                webDialog: new WaitingDialog(),
                sound: new RecordingSoundStore(new SettingsFile(Path.Combine(root, "settings.json")),
                    new LiveSettings(new ShareSettings { Recording = new RecordingSettings { Audio = new AudioSettings { Microphone = true } } })));
            BarWindow window = new(bar, new BarPositionStore(new SettingsFile(Path.Combine(root, "position.json"))), new BarSettings(), exclusion);
            window.Summon();
            Dispatcher.UIThread.RunJobs();
            test(window, bar);
            window.Quit();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A web window that stays open until it is canceled.</summary>
    private sealed class WaitingDialog : IWebCaptureDialog
    {
        public Task<CaptureResult> RunAsync(IWebPageCapture capture, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<CaptureResult> closed = new();
            cancellationToken.Register(() => closed.TrySetCanceled(cancellationToken));
            return closed.Task;
        }
    }

    private sealed class RecordingExclusion : ICaptureExclusion
    {
        public List<nint> Windows { get; } = [];

        public bool Exclude(nint window)
        {
            Windows.Add(window);
            return true;
        }

        public bool Include(nint window) => true;
    }
}
