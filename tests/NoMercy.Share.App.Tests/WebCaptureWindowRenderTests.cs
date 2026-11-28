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
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Tests;

namespace NoMercy.Share.App.Tests;

/// <summary>The web capture window in the real dark theme: the bar's surface, rows of choice pills, one focus ring and the keys.</summary>
public sealed class WebCaptureWindowRenderTests
{
    private const string RenderFolderVariable = "SHARE_RENDER_DIR";
    private const string LongFailure = "No Edge or Chrome was found. Set WebCapture.BrowserPath in settings.json to the browser you want to use, then try again.";

    [Fact]
    public void TheWindowIsOneBarSurfaceWithoutAnOsFrame() => WithWindow((window, _, _) =>
    {
        Assert.Equal(Avalonia.Controls.WindowDecorations.None, window.WindowDecorations);
        Border? surface = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "Surface");
        Assert.NotNull(surface);
        Assert.Equal(Brush(window, "Share-Bar-Brush"), (surface.Background as ISolidColorBrush)?.Color);
        Assert.Equal(Brush(window, "Share-Ring-Brush"), (surface.BorderBrush as ISolidColorBrush)?.Color);
        Assert.NotEqual(default, surface.BoxShadow);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Capture a web page");
        Render(window, "web-window-default.png");
    });

    [Fact]
    public void NoChoiceIsADropDown() => WithWindow((window, _, _) =>
        Assert.Empty(window.GetVisualDescendants().OfType<ComboBox>()));

    [Theory]
    [InlineData(WebCaptureKind.FullPage)]
    [InlineData(WebCaptureKind.Viewport)]
    [InlineData(WebCaptureKind.Element)]
    public void BothChoiceRowsShowEveryChoiceUnclipped(WebCaptureKind kind) => WithWindow((window, model, capture) =>
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
            Render(window, "web-window-element-errors.png");
        }

        model.Address = "https://example.test/";
        model.Selector = "#header";
        capture.Throws = new WebCaptureException(WebCaptureField.None, LongFailure);
        Pump(model.CaptureAsync());
        Assert.Equal(LongFailure, model.FailureText);
        AssertRowsAndTexts(window);
    });

    [Fact]
    public void PickingAPillUpdatesTheViewModel() => WithWindow((window, model, _) =>
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
    public void TabReachesEveryControlAndArrowsMoveInsideARow() => WithWindow((window, model, _) =>
    {
        Assert.Equal("Address", FocusedName(window));
        Press(window, PhysicalKey.Tab);
        Assert.Equal("Full page", FocusedName(window));
        Press(window, PhysicalKey.ArrowRight);
        Assert.Equal(WebCaptureKind.Viewport, model.SelectedMode.Kind);

        List<string?> reached = [];
        for (int step = 0; step < 4; step++)
        {
            Press(window, PhysicalKey.Tab);
            reached.Add(FocusedName(window));
        }

        Assert.Equal(["Desktop (1280 px)", "Cancel", "Capture"], reached.Take(3));
        Assert.Equal("Close", reached[3]);
    });

    [Fact]
    public void EveryFocusedControlShowsExactlyOneRing() => WithWindow((window, model, _) =>
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
    public void TheAddressHasFocusAndEnterCaptures() => WithWindow((window, model, capture) =>
    {
        Assert.Equal("Address", FocusedName(window));
        Assert.Equal("https://example.test/articles/made-up", model.Address);

        Press(window, PhysicalKey.Enter);

        Assert.Single(capture.Requests);
        Assert.False(window.IsVisible);
    });

    [Fact]
    public void EnterCapturesFromAChoicePill() => WithWindow((window, _, capture) =>
    {
        Pill(window, "Mode", "Full page").Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);

        Assert.Single(capture.Requests);
    });

    [Fact]
    public void EscapeCancels() => WithWindow((window, _, capture) =>
    {
        Press(window, PhysicalKey.Escape);

        Assert.False(window.IsVisible);
        Assert.Empty(capture.Requests);
    });

    [Fact]
    public void TheCrossCancels() => WithWindow((window, _, capture) =>
    {
        Button? cross = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => button.GetVisualDescendants().OfType<MoooomIcon>().Any(icon => icon.Kind == "cross"));
        Assert.NotNull(cross);
        Assert.Equal("Close", AutomationProperties.GetName(cross));

        Click(window, cross);

        Assert.False(window.IsVisible);
        Assert.Empty(capture.Requests);
    });

    private static void WithWindow(Action<WebCaptureWindow, WebCaptureViewModel, FakeWebPageCapture> test) => HeadlessApp.Run(() =>
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        FakeWebPageCapture capture = new();
        WebCaptureViewModel model = new(capture, new WebCaptureSettings(), "https://example.test/articles/made-up");
        WebCaptureWindow window = new(model);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            test(window, model, capture);
        }
        finally
        {
            window.Close();
        }
    });

    private static void AssertRowsAndTexts(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        foreach ((string row, int count) in new[] { ("Mode", 3), ("Width", 2) })
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
