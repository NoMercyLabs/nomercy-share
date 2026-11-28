using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;
using NoMercy.Share.Core.Tests;

namespace NoMercy.Share.App.Tests;

public sealed class BarMenuRenderTests
{
    private const string RenderFolderVariable = "SHARE_RENDER_DIR";
    private const double MinimumHitSize = 36;

    [Fact]
    public void TheScreenshotMenuOffersAreaWindowAndFullScreen() => HeadlessApp.Run(() =>
        RenderMenu(BarAction.Screenshot, "ScreenshotMenu", ["Area", "Window", "Full screen"], "share-menu-screenshot.png"));

    [Fact]
    public void TheRecordMenuOffersTargetsSoundSwitchesAndStart() => HeadlessApp.Run(() =>
        RenderMenu(BarAction.Record, "RecordMenu", ["Area", "Window", "Full screen", "Microphone", "Desktop audio", "Start"], "share-menu-record.png"));

    [Fact]
    public void TheBarKeepsItselfOutOfCapturesOnceItIsOpen() => HeadlessApp.Run(() =>
    {
        RecordingExclusion exclusion = new();
        WithBar(exclusion, (window, _) =>
        {
            window.Summon();
            Dispatcher.UIThread.RunJobs();

            Assert.Single(exclusion.Windows);
        });
    });

    [Fact]
    public void TheCrossClosesAnOpenMenu() => HeadlessApp.Run(() => WithBar(null, (window, bar) =>
    {
        window.Summon();
        Pump(bar.RunAsync(BarAction.Screenshot));
        Assert.Equal(BarMenu.Screenshot, bar.Menu);

        window.Dismiss();

        Assert.Equal(BarMenu.None, bar.Menu);
    }));

    private static void RenderMenu(BarAction open, string panel, string[] names, string picture) => WithBar(null, (window, bar) =>
    {
        window.Summon();
        Pump(bar.RunAsync(open));
        Dispatcher.UIThread.RunJobs();

        Border menu = window.FindControl<Border>(panel)!;
        Assert.True(menu.IsEffectivelyVisible);
        Button[] buttons = [.. menu.GetVisualDescendants().OfType<Button>()];
        Assert.Equal(names, buttons.Select(AutomationProperties.GetName));
        Assert.All(buttons, button => Assert.True(button.Bounds.Height >= MinimumHitSize && button.Bounds.Width >= MinimumHitSize));

        using Avalonia.Media.Imaging.WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable(RenderFolderVariable) is { Length: > 0 } folder)
        {
            frame.Save(Path.Combine(folder, picture));
        }
    });

    private static void Pump(Task task)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.True(task.IsCompletedSuccessfully, "the UI work did not finish");
    }

    private static void WithBar(ICaptureExclusion? exclusion, Action<BarWindow, BarViewModel> test)
    {
        string root = Path.Combine(Path.GetTempPath(), $"share-menu-render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            BarViewModel bar = new(
                new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
                new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
                recorder: new FakeScreenRecorder(),
                sound: new RecordingSoundStore(new SettingsFile(Path.Combine(root, "settings.json")),
                    new LiveSettings(new ShareSettings { Recording = new RecordingSettings { Audio = new AudioSettings { Microphone = true } } })));
            BarWindow window = new(bar, new BarPositionStore(new SettingsFile(Path.Combine(root, "position.json"))), new BarSettings(), exclusion);
            test(window, bar);
            window.Quit();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
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
    }
}
