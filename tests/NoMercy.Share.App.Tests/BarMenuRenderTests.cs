using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
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

public sealed class BarMenuRenderTests
{
    private const string RenderFolderVariable = "SHARE_RENDER_DIR";
    private const double MinimumHitSize = 36;
    private const double MaximumRecordMenuWidth = 420;

    [Fact]
    public void TheScreenshotMenuOffersAreaWindowAndFullScreen() => HeadlessApp.Run(() =>
        RenderMenu(BarAction.Screenshot, "ScreenshotMenu", ["Area", "Window", "Full screen"], "narrow-screenshot-menu.png"));

    [Fact]
    public void TheRecordMenuOffersTargetsSoundSwitchesAndStart() => HeadlessApp.Run(() =>
        RenderMenu(BarAction.Record, "RecordMenu", ["Area", "Window", "Full screen", "Microphone", "Desktop audio", "Start"], "narrow-record-menu.png"));

    [Theory]
    [InlineData(BarAction.Screenshot, "ScreenshotMenu")]
    [InlineData(BarAction.Record, "RecordMenu")]
    public void AMenuShowsNoTextExceptStart(BarAction open, string panel) => HeadlessApp.Run(() => WithMenu(open, panel, (_, menu) =>
    {
        string?[] texts = [.. menu.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text)];

        Assert.Equal(open == BarAction.Record ? ["Start"] : [], texts);
    }));

    [Theory]
    [InlineData(BarAction.Screenshot, "ScreenshotMenu", new[] { "Area", "Window", "Full screen" })]
    [InlineData(BarAction.Record, "RecordMenu", new[] { "Area", "Window", "Full screen", "Microphone: On", "Desktop audio: Off", "Start" })]
    public void EveryMenuButtonHasATooltip(BarAction open, string panel, string[] tips) => HeadlessApp.Run(() => WithMenu(open, panel, (_, menu) =>
    {
        Assert.Equal(tips, menu.GetVisualDescendants().OfType<Button>().Select(button => ToolTip.GetTip(button) as string));
    }));

    [Fact]
    public void ASoundSwitchThatIsOffShowsACrossedOutIcon() => HeadlessApp.Run(() => WithMenu(BarAction.Record, "RecordMenu", (_, menu) =>
    {
        string[] icons = [.. menu.GetVisualDescendants().OfType<MoooomIcon>().Where(icon => icon.IsEffectivelyVisible).Select(icon => icon.Kind)];

        Assert.Equal(["crossHair", "macOsWindow", "monitor", "mic", "volumeMuted"], icons);
    }));

    [Fact]
    public void TheRecordMenuIsNarrow() => HeadlessApp.Run(() => WithMenu(BarAction.Record, "RecordMenu", (_, menu) =>
    {
        Assert.True(menu.Bounds.Width < MaximumRecordMenuWidth, $"the record menu is {menu.Bounds.Width} units wide");
    }));

    private static void WithMenu(BarAction open, string panel, Action<BarWindow, Border> test) => WithBar(null, (window, bar) =>
    {
        window.Summon();
        Pump(bar.RunAsync(open));
        Dispatcher.UIThread.RunJobs();

        test(window, window.FindControl<Border>(panel)!);
    });

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
        Assert.All(menu.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible), text =>
        {
            // A clipped label is narrower than the words need, or ends outside the window.
            Assert.True(text.Bounds.Width >= Math.Floor(text.TextLayout.Width), $"{text.Text} is clipped");
            Assert.True(text.TranslatePoint(new Point(text.Bounds.Width, 0), window)!.Value.X <= window.Bounds.Width, $"{text.Text} ends outside the window");
        });

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

        public bool Include(nint window) => true;
    }
}
