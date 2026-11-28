using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoMercy.Share.App.Icons;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;
using NoMercy.Share.Core.Tests;

namespace NoMercy.Share.App.Tests;

/// <summary>The web panel under the bar in the real dark theme: the bar's surface, rows of choice pills, one focus ring and the keys.</summary>
public sealed class WebCapturePanelRenderTests
{
    private const string RenderFolderVariable = "SHARE_RENDER_DIR";
    private const string LongFailure = "No Edge or Chrome was found. Set WebCapture.BrowserPath in settings.json to the browser you want to use, then try again.";

    [Fact]
    public void TheGlobeShowsThePanelUnderTheRowInTheSameWindow() => WithWindow((window, _, _, _) =>
    {
        Border surface = Surface(window);
        Border pill = window.FindControl<Border>("Pill")!;
        window.UpdateLayout();
        Point under = surface.TranslatePoint(default, window)!.Value;
        Point row = pill.TranslatePoint(default, window)!.Value;

        Assert.Same(window, TopLevel.GetTopLevel(surface));
        Assert.Equal(row.X, under.X);
        Assert.Equal(row.Y + pill.Bounds.Height + 8, under.Y);
        Assert.Equal(Brush(window, "Share-Bar-Brush"), (surface.Background as ISolidColorBrush)?.Color);
        Assert.Equal(Brush(window, "Share-Ring-Brush"), (surface.BorderBrush as ISolidColorBrush)?.Color);
        Assert.Equal(pill.BoxShadow, surface.BoxShadow);
        Assert.Contains(Texts(window), text => text == "Pages open signed out.");
        Assert.DoesNotContain(Texts(window), text => text == "Capture a web page");
        Assert.DoesNotContain(PanelOf(window).GetVisualDescendants().OfType<Button>(), button => AutomationProperties.GetName(button) is "Cancel" or "Close");
        Render(window, "webpanel-default.png");
    });

    [Theory]
    [InlineData("Screenshot")]
    [InlineData("Record the screen")]
    public void TheCameraAndTheVideoButtonCloseThePanel(string mode) => WithWindow((window, _, _, bar) =>
    {
        Click(window, BarButton(window, mode));

        Assert.Null(bar.WebPanel);
        Assert.Null(FindPanel(window));
        Assert.True(window.IsVisible);
    });

    [Fact]
    public void TheCrossOfTheBarClosesThePanelAndHidesTheBar() => WithWindow((window, _, capture, bar) =>
    {
        Click(window, BarButton(window, "Close NoMercy Share"));

        Assert.False(window.IsVisible);
        Assert.Null(bar.WebPanel);
        Assert.Empty(capture.Requests);
    });

    [Fact]
    public void ClosingTheWindowSomeOtherWayCancelsThePanel() => WithWindow((window, _, _, bar) =>
    {
        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(bar.WebPanel);
    });

    [Fact]
    public void AfterTheRecentListThereIsNoPanel() => WithWindow((window, _, _, bar) =>
    {
        Click(window, BarButton(window, "Recent links"));

        Assert.True(bar.IsRecentOpen);
        Assert.Null(FindPanel(window));
    });

    [Fact]
    public void WhileBusyTheInputsAreDisabledAndEscapeStillWorks()
    {
        TaskCompletionSource<CaptureResult> loading = new();
        WithWindow((window, model, _, bar) =>
        {
            Control address = Named(window, "Address");
            Press(window, PhysicalKey.Enter);

            Assert.True(model.IsBusy);
            Assert.False(address.IsEffectivelyEnabled);
            Assert.False(Named(window, "Capture").IsEffectivelyEnabled);
            Assert.False(Pill(window, "Width", "Phone (390 px)").IsEffectivelyEnabled);
            Assert.True(BarButton(window, "Screenshot").IsEffectivelyEnabled);

            Press(window, PhysicalKey.Escape);

            Assert.Null(bar.WebPanel);
            Assert.True(bar.Row.IsScreenshotMode);
            Assert.True(window.IsVisible);
        }, new PendingWebCapture(loading));
    }

    [Fact]
    public void NoChoiceIsADropDown() => WithWindow((window, _, _, _) =>
        Assert.Empty(window.GetVisualDescendants().OfType<ComboBox>()));

    [Theory]
    [InlineData(WebCaptureKind.FullPage)]
    [InlineData(WebCaptureKind.Viewport)]
    [InlineData(WebCaptureKind.Element)]
    public void BothChoiceRowsShowEveryChoiceUnclipped(WebCaptureKind kind) => WithWindow((window, model, capture, _) =>
    {
        model.SelectedMode = model.Modes.Single(mode => mode.Kind == kind);
        Dispatcher.UIThread.RunJobs();
        AssertRowsAndTexts(window);

        model.Address = "example.test";
        Pump(model.CaptureAsync());
        AssertRowsAndTexts(window);
        if (kind == WebCaptureKind.Element)
        {
            Assert.NotNull(model.SelectorError);
            Render(window, "webpanel-element-errors.png");
        }

        model.Address = "https://example.test/";
        model.Selector = "#header";
        capture.Throws = new WebCaptureException(WebCaptureField.None, LongFailure);
        Pump(model.CaptureAsync());
        Assert.Equal(LongFailure, model.FailureText);
        AssertRowsAndTexts(window);
    });

    [Fact]
    public void PickingAPillUpdatesTheViewModel() => WithWindow((window, model, _, _) =>
    {
        Click(window, Pill(window, "Mode", "One element"));
        Click(window, Pill(window, "Width", "Phone (390 px)"));

        Assert.Equal(WebCaptureKind.Element, model.SelectedMode.Kind);
        Assert.Equal(WebCaptureDevice.Phone, model.SelectedDevice.Device);
        Assert.True(IsSelected(Pill(window, "Mode", "One element")));
        Assert.False(IsSelected(Pill(window, "Mode", "Full page")));
        Assert.Equal(Brush(window, "Nm-Accent-Brush"), IconColor(Pill(window, "Mode", "One element")));
        Assert.NotEqual(Brush(window, "Nm-Accent-Brush"), IconColor(Pill(window, "Mode", "Full page")));
    });

    [Fact]
    public void SixShortPillsFitThePanelAndNameTheirPixelWidth() => WithWindow((window, _, _, _) =>
    {
        window.UpdateLayout();
        ListBoxItem[] pills = [.. Row(window, "Width").GetVisualDescendants().OfType<ListBoxItem>()];

        Assert.Equal(480, Surface(window).Bounds.Width);
        Assert.Equal(
            ["Phone", "Tablet", "Desktop", "1080p", "1440p", "4K"],
            pills.Select(pill => pill.GetVisualDescendants().OfType<TextBlock>().Single().Text));
        string[] full = ["Phone (390 px)", "Tablet (834 px)", "Desktop (1280 px)", "1080p (1920 px)", "1440p (2560 px)", "4K (3840 px)"];
        Assert.Equal(full, pills.Select(AutomationProperties.GetName));
        Assert.Equal(full, pills.Select(pill => ToolTip.GetTip(pill) as string));
        Assert.All(pills, pill => Assert.True(pill.IsEffectivelyVisible && Inside(pill, window), $"{AutomationProperties.GetName(pill)} is not wholly in the window"));
        AssertRowsAndTexts(window);
        Render(window, "web-widths.png");
    });

    [Fact]
    public void TheArrowKeysMoveThroughAllSixWidths() => WithWindow((window, model, _, _) =>
    {
        Pill(window, "Width", "Desktop (1280 px)").Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.ArrowLeft);
        Press(window, PhysicalKey.ArrowLeft);

        List<int> reached = [model.SelectedDevice.Width];
        for (int step = 0; step < 5; step++)
        {
            Press(window, PhysicalKey.ArrowRight);
            reached.Add(model.SelectedDevice.Width);
        }

        Assert.Equal([390, 834, 1280, 1920, 2560, 3840], reached);
    });

    [Fact]
    public void TabReachesEveryControlAndArrowsMoveInsideARow() => WithWindow((window, model, _, _) =>
    {
        Assert.Equal("Address", FocusedName(window));
        Press(window, PhysicalKey.Tab);
        Assert.Equal("Full page", FocusedName(window));
        Press(window, PhysicalKey.ArrowRight);
        Assert.Equal(WebCaptureKind.Viewport, model.SelectedMode.Kind);

        List<string?> reached = [];
        for (int step = 0; step < 2; step++)
        {
            Press(window, PhysicalKey.Tab);
            reached.Add(FocusedName(window));
        }

        Assert.Equal(["Desktop (1280 px)", "Capture"], reached);
    });

    [Fact]
    public void EveryFocusedControlShowsExactlyOneRing() => WithWindow((window, model, _, _) =>
    {
        model.SelectedMode = model.Modes.Single(mode => mode.Kind == WebCaptureKind.Element);
        Dispatcher.UIThread.RunJobs();
        List<string> problems = [];
        for (int step = 0; step < 7; step++)
        {
            Press(window, PhysicalKey.Tab);
            Control focused = (Control)window.FocusManager!.GetFocusedElement()!;
            int rings = Rings(window);
            if (rings != 1 || focused.FocusAdorner is not null)
            {
                problems.Add($"{FocusedName(window)}: {rings} rings, adorner {focused.FocusAdorner is not null}");
            }

            if (FocusedName(window) == "One element")
            {
                Render(window, "web-window-focus.png");
            }
        }

        Assert.True(problems.Count == 0, string.Join("; ", problems));
    });

    [Fact]
    public void TheAddressHasFocusAndEnterCapturesAndClosesThePanel() => WithWindow((window, model, capture, bar) =>
    {
        Assert.Equal("Address", FocusedName(window));
        Assert.Equal("https://example.test/articles/made-up", model.Address);

        Press(window, PhysicalKey.Enter);

        Assert.Single(capture.Requests);
        Assert.Null(bar.WebPanel);
        Assert.Null(FindPanel(window));
        Assert.NotNull(bar.Choice);
    });

    [Theory]
    [InlineData("Full page")]
    [InlineData("Capture")]
    public void EnterCapturesFromAnyControl(string name) => WithWindow((window, _, capture, _) =>
    {
        Named(window, name).Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);

        Assert.Single(capture.Requests);
    });

    [Fact]
    public void EscapeClosesThePanelThenHidesTheBar() => WithWindow((window, _, capture, bar) =>
    {
        Press(window, PhysicalKey.Escape);

        Assert.Null(bar.WebPanel);
        Assert.True(bar.Row.IsScreenshotMode);
        Assert.True(window.IsVisible);

        Press(window, PhysicalKey.Escape);

        Assert.False(window.IsVisible);
        Assert.Empty(capture.Requests);
    });

    /// <summary>The bar with its web panel opened by the globe, an https address on the clipboard.</summary>
    [Fact]
    public void ABarAtTheBottomOfTheScreenMovesUpForThePanelAndBackAfterIt() => WithWindow((window, _, _, bar) =>
    {
        bar.Escape();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        PixelRect area = window.Screens.Primary!.WorkingArea;
        PixelPoint low = new(area.X + 20, area.Bottom - (int)Math.Ceiling(window.Bounds.Height * window.RenderScaling));
        window.Position = low;
        Dispatcher.UIThread.RunJobs();

        _ = bar.RunAsync(BarAction.WebPage);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.Position.Y + window.Bounds.Height * window.RenderScaling <= area.Bottom + 0.5,
            $"the panel ends below the screen: top {window.Position.Y}, height {window.Bounds.Height}, screen bottom {area.Bottom}");

        bar.Escape();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(low, window.Position);
    });

    private static void WithWindow(Action<BarWindow, WebCaptureViewModel, FakeWebPageCapture, BarViewModel> test, IWebPageCapture? web = null) =>
        HeadlessApp.Run(() =>
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            string root = Path.Combine(Path.GetTempPath(), $"share-panel-render-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            FakeWebPageCapture capture = new();
            BarViewModel bar = new(
                new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
                new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
                recorder: new FakeScreenRecorder(),
                web: web ?? capture,
                recent: new RecentLinks(Path.Combine(root, "recent.json"), new RecentSettings()),
                clipboard: new FakeClipboard { Text = "https://example.test/articles/made-up" });
            BarWindow window = new(bar, new BarPositionStore(new SettingsFile(Path.Combine(root, "position.json"))), new BarSettings());
            try
            {
                window.Summon();
                Dispatcher.UIThread.RunJobs();
                _ = bar.RunAsync(BarAction.WebPage);
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(bar.WebPanel);
                test(window, bar.WebPanel, capture, bar);
            }
            finally
            {
                window.Quit();
                Directory.Delete(root, recursive: true);
            }
        });

    private static Control? FindPanel(Window window) =>
        window.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.GetType().Name == "WebCapturePanel");

    private static Control PanelOf(Window window)
    {
        Control? panel = FindPanel(window);
        Assert.True(panel is not null, "the bar shows no web panel");
        return panel;
    }

    private static Border Surface(Window window) => PanelOf(window).GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Surface");

    private static IEnumerable<string?> Texts(Window window) =>
        PanelOf(window).GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text);

    private static Control Named(Window window, string name) =>
        PanelOf(window).GetVisualDescendants().OfType<Control>().First(control => AutomationProperties.GetName(control) == name);

    private static Button BarButton(Window window, string name) =>
        window.FindControl<Border>("Pill")!.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == name);

    private sealed class PendingWebCapture(TaskCompletionSource<CaptureResult> loading) : IWebPageCapture
    {
        public Task<CaptureResult> CaptureAsync(WebCaptureRequest request, CancellationToken cancellationToken = default) =>
            loading.Task.WaitAsync(cancellationToken);
    }

    private static void AssertRowsAndTexts(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        foreach ((string row, int count) in new[] { ("Mode", 3), ("Width", 6) })
        {
            ListBoxItem[] pills = [.. Row(window, row).GetVisualDescendants().OfType<ListBoxItem>()];
            Assert.Equal(count, pills.Length);
            Assert.All(pills, pill => Assert.True(pill.IsEffectivelyVisible && Inside(pill, window), $"{AutomationProperties.GetName(pill)} is not wholly in the window"));
        }

        Assert.All(window.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text)), text =>
        {
            Assert.True(text.Bounds.Width >= Math.Floor(text.TextLayout.Width) && text.TextLayout.TextLines.All(line => !line.HasCollapsed), $"{text.Text} is clipped");
            Assert.True(Inside(text, window), $"{text.Text} ends outside the window");
        });
    }

    private static bool Inside(Control control, Window window) =>
        control.TranslatePoint(default, window) is { } start && start.X >= 0 && start.Y >= 0
        && start.X + control.Bounds.Width <= window.ClientSize.Width + 0.5 && start.Y + control.Bounds.Height <= window.ClientSize.Height + 0.5;

    private static ListBox Row(Window window, string name)
    {
        ListBox? row = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(list => AutomationProperties.GetName(list) == name);
        Assert.True(row is not null, $"no choice row named {name}");
        return row;
    }

    private static ListBoxItem Pill(Window window, string row, string name) =>
        Row(window, row).GetVisualDescendants().OfType<ListBoxItem>().Single(item => AutomationProperties.GetName(item) == name);

    private static bool IsSelected(ListBoxItem pill) => ((ISelectionItemProvider)ControlAutomationPeer.CreatePeerForElement(pill)).IsSelected;

    private static Color? IconColor(ListBoxItem pill) => (pill.GetVisualDescendants().OfType<MoooomIcon>().Single().Stroke as ISolidColorBrush)?.Color;

    private static int Rings(Window window)
    {
        Color accent = Brush(window, "Nm-Accent-Brush");
        return window.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible).Count(control => control switch
        {
            Border border => border.BorderThickness != default && (border.BorderBrush as ISolidColorBrush)?.Color == accent,
            ContentPresenter presenter => presenter.BorderThickness != default && (presenter.BorderBrush as ISolidColorBrush)?.Color == accent,
            _ => false
        });
    }

    private static string? FocusedName(Window window) =>
        window.FocusManager.GetFocusedElement() is Control focused ? AutomationProperties.GetName(focused) : null;

    private static Color Brush(Window window, string key) => ((ISolidColorBrush)window.FindResource(window.ActualThemeVariant, key)!).Color;

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        if (window.IsVisible) // Enter and Escape close the window, and a closed one takes no release
        {
            window.KeyReleaseQwerty(key, RawInputModifiers.None);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Control control)
    {
        Point middle = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Pump(Task task)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.True(task.IsCompletedSuccessfully, "the UI work did not finish");
    }

    private static void Render(Window window, string picture)
    {
        using Avalonia.Media.Imaging.WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable(RenderFolderVariable) is { Length: > 0 } folder)
        {
            frame.Save(Path.Combine(folder, picture));
        }
    }
}
