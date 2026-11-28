using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;
using NoMercy.Share.Core.Tests;

namespace NoMercy.Share.App.Tests;

/// <summary>The settings window in the real dark theme: every section draws, no control sticks out of the window, and Cancel and Save stay in view.</summary>
public sealed class SettingsWindowRenderTests
{
    private const string RenderFolderVariable = "SHARE_RENDER_DIR";

    [Theory]
    [InlineData(SettingsSection.General)]
    [InlineData(SettingsSection.Sharing)]
    [InlineData(SettingsSection.Hotkeys)]
    [InlineData(SettingsSection.Recording)]
    public void EverySectionFitsAndTheFooterStaysVisible(SettingsSection section) => HeadlessApp.Run(() =>
    {
        (SettingsWindow window, string root) = Open(section, width: null);
        try
        {
            List<string> problems = CutOff(window);
            Assert.True(problems.Count == 0, string.Join("; ", problems));

            using Avalonia.Media.Imaging.WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            if (Environment.GetEnvironmentVariable(RenderFolderVariable) is { Length: > 0 } folder)
            {
                frame.Save(Path.Combine(folder, $"share-settings-{section.ToString().ToLowerInvariant()}.png"));
            }
        }
        finally
        {
            window.Close();
            Directory.Delete(root, recursive: true);
        }
    });

    [Theory]
    [InlineData(SettingsSection.General)]
    [InlineData(SettingsSection.Sharing)]
    [InlineData(SettingsSection.Hotkeys)]
    [InlineData(SettingsSection.Recording)]
    public void EveryPageFitsWithoutScrollingAndItsTitleSitsLevelWithTheCross(SettingsSection section) => HeadlessApp.Run(() =>
    {
        (SettingsWindow window, string root) = Open(section, width: null);
        try
        {
            ScrollViewer page = window.FindControl<ScrollViewer>("Page")!;
            Assert.True(page.Extent.Height <= page.Viewport.Height + 0.5, $"{section} needs {page.Extent.Height}, has {page.Viewport.Height}");

            TextBlock title = Assert.Single(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Classes.Contains("section") && text.IsEffectivelyVisible);
            Button cross = Assert.Single(window.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("close"));
            Assert.Equal(CentreY(cross, window), CentreY(title, window), tolerance: 1);
        }
        finally
        {
            window.Close();
            Directory.Delete(root, recursive: true);
        }
    });

    [Fact]
    public void TheSidebarButtonsHaveAGapBetweenThem() => HeadlessApp.Run(() => With(SettingsSection.General, window =>
    {
        ListBox rail = window.GetVisualDescendants().OfType<ListBox>().Single(list => list.Classes.Contains("rail"));
        Rect[] pills = [.. rail.GetVisualDescendants().OfType<ListBoxItem>().Select(item =>
        {
            Control pill = item.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(part => part.Name == "PART_ContentPresenter");
            return new Rect(pill.TranslatePoint(default, window)!.Value, pill.Bounds.Size);
        })];

        Assert.All(pills.Zip(pills.Skip(1)), pair => Assert.True(pair.Second.Top - pair.First.Bottom >= 4, $"gap {pair.Second.Top - pair.First.Bottom}"));
    }));

    [Fact]
    public void TheFirstPageHoldsTheSettingsThatChangeOften() => HeadlessApp.Run(() => With(SettingsSection.General, window =>
    {
        string[] shown = [..window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && control is ToggleSwitch or ComboBox or TextBox)
            .Select(AutomationName)];

        Assert.Equal(["Show bar in captures", "Default expiry", "Record system sound", "Record microphone", "Max resolution", "Max size"], shown);
    }));

    [Fact]
    public void TheStorageKeysStayHiddenUntilTheirEyeIsPressed() => HeadlessApp.Run(() => With(SettingsSection.Sharing, window =>
    {
        TextBox[] secrets = [.. window.GetVisualDescendants().OfType<TextBox>().Where(box => box.IsEffectivelyVisible && box.PasswordChar != default)];
        Assert.Equal(["Endpoint", "Access key id", "Secret access key"], secrets.Select(AutomationName));
        Assert.All(secrets, box => Assert.False(box.RevealPassword));

        Button eye = secrets[1].GetVisualDescendants().OfType<Button>().Single(button => AutomationName(button) == "Show or hide");
        eye.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(secrets[1].RevealPassword);
        Assert.False(secrets[0].RevealPassword);
    }));

    private static void With(SettingsSection section, Action<SettingsWindow> test)
    {
        (SettingsWindow window, string root) = Open(section, width: null);
        try
        {
            test(window);
        }
        finally
        {
            window.Close();
            Directory.Delete(root, recursive: true);
        }
    }

    private static double CentreY(Control control, Window window) => control.TranslatePoint(new Point(0, control.Bounds.Height / 2), window)!.Value.Y;

    [Fact]
    public void ThePlantedTooSmallWindowIsReportedAsCutOff() => HeadlessApp.Run(() =>
    {
        (SettingsWindow window, string root) = Open(SettingsSection.Sharing, width: 150);
        try
        {
            Assert.NotEmpty(CutOff(window));
        }
        finally
        {
            window.Close();
            Directory.Delete(root, recursive: true);
        }
    });

    [Fact]
    public void TheGeneralSectionHasTheShowBarSwitchOffAtFirst() => HeadlessApp.Run(() => WithGeneralSection((window, _, _) =>
    {
        ToggleSwitch shown = ShowBarSwitch(window);
        Assert.True(shown.IsEffectivelyVisible);
        Assert.False(shown.IsChecked);

        using Avalonia.Media.Imaging.WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable(RenderFolderVariable) is { Length: > 0 } folder)
        {
            frame.Save(Path.Combine(folder, "settings-general.png"));
        }
    }));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheShowBarSwitchIsKeptBySaveAndDroppedByCancel(bool save) => HeadlessApp.Run(() => WithGeneralSection((window, model, file) =>
    {
        ShowBarSwitch(window).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.False(file.Load().Bar.ShowInCaptures);

        if (save)
        {
            model.Save();
        }
        else
        {
            model.Cancel();
        }

        Assert.Equal(save, file.Load().Bar.ShowInCaptures);
    }));

    private static ToggleSwitch ShowBarSwitch(SettingsWindow window) => Assert.Single(
        window.GetVisualDescendants().OfType<ToggleSwitch>(), toggle => AutomationName(toggle) == "Show bar in captures");

    private static void WithGeneralSection(Action<SettingsWindow, SettingsViewModel, SettingsFile> test)
    {
        (SettingsWindow window, string root) = Open(SettingsSection.General, width: null);
        try
        {
            test(window, (SettingsViewModel)window.DataContext!, new SettingsFile(Path.Combine(root, "settings.json")));
        }
        finally
        {
            window.Close();
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The bar's rules: no OS frame, one surface with the bar's fill, ring and shadow, no box inside a box, and no accent at rest.</summary>
    [Theory]
    [InlineData(SettingsSection.General)]
    [InlineData(SettingsSection.Sharing)]
    [InlineData(SettingsSection.Hotkeys)]
    [InlineData(SettingsSection.Recording)]
    public void TheWindowFollowsTheBarsRules(SettingsSection section) => HeadlessApp.Run(() =>
    {
        (SettingsWindow window, string root) = Open(section, width: null);
        try
        {
            Assert.Equal(Avalonia.Controls.WindowDecorations.None, window.WindowDecorations);
            Border surface = window.GetVisualDescendants().OfType<Border>().First(border => border.Name == "Surface");
            Assert.Equal(Color(window, "Share-Bar-Brush"), (surface.Background as Avalonia.Media.ISolidColorBrush)?.Color);
            Assert.Equal(Color(window, "Share-Ring-Brush"), (surface.BorderBrush as Avalonia.Media.ISolidColorBrush)?.Color);
            Assert.NotEqual(default, surface.BoxShadow);

            List<string> boxes = [.. window.GetVisualDescendants().OfType<Border>().Where(border => border.IsEffectivelyVisible && border.Classes.Contains("card")).Select(border => border.Name ?? "card")];
            Assert.True(boxes.Count == 0, $"box inside the surface: {string.Join(", ", boxes)}");

            Avalonia.Media.Color accent = Color(window, "Nm-Accent-Brush");
            List<string> lit = [.. window.GetVisualDescendants().OfType<Control>()
                .Where(control => control.IsEffectivelyVisible && control is not ToggleSwitch && !control.GetVisualAncestors().OfType<ToggleSwitch>().Any())
                .Where(control => Fill(control) is Avalonia.Media.ISolidColorBrush { } brush && brush.Color == accent)
                .Select(control => $"{control.GetType().Name} {control.Name}")];
            Assert.True(lit.Count == 0, $"accent at rest: {string.Join(", ", lit)}");
        }
        finally
        {
            window.Close();
            Directory.Delete(root, recursive: true);
        }
    });

    private static Avalonia.Media.Color Color(SettingsWindow window, string key) =>
        ((Avalonia.Media.ISolidColorBrush)window.FindResource(window.ActualThemeVariant, key)!).Color;

    private static Avalonia.Media.IBrush? Fill(Control control) => control switch
    {
        Border border => border.Background,
        Avalonia.Controls.Presenters.ContentPresenter presenter => presenter.Background,
        Panel panel => panel.Background,
        _ => null
    };

    private static (SettingsWindow Window, string Root) Open(SettingsSection section, double? width)
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        string root = Path.Combine(Path.GetTempPath(), $"share-settings-render-{Guid.NewGuid():N}");
        SettingsFile file = new(Path.Combine(root, "settings.json"));
        file.Save(new ShareSettings
        {
            Storage = new StorageSettings { Provider = "r2", Endpoint = "https://storage.example.test", Bucket = "made-up-bucket", PublicBaseUrl = "https://share.example.test", CredentialName = "made-up" },
            Expiry = new ExpirySettings { DefaultDays = 7 }
        });
        BarViewModel bar = new(new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings()),
            new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()));
        // A stored made-up key, so Save has nothing to refuse.
        FakeCredentialStore credentials = new();
        credentials.Stored["made-up"] = new StoredCredential("made-up-key-id", "made-up-secret");
        SettingsViewModel model = new(file, new LiveSettings(file.Load()), credentials, new HotkeyBinder(new FakeGlobalHotkeys(), bar, new HotkeySettings()), ["r2", "s3", "discord"])
        {
            SelectedSection = section
        };
        SettingsWindow window = new(model);
        if (width is { } planted)
        {
            window.MinWidth = 0;
            window.Width = planted;
        }

        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, root);
    }

    /// <summary>What sticks out of the window sideways, plus the footer buttons when they are not wholly in view. Rows below the fold of the scrolling content are fine.</summary>
    private static List<string> CutOff(SettingsWindow window)
    {
        List<string> problems = [];
        Rect client = new(window.ClientSize);
        foreach (Control control in window.GetVisualDescendants().OfType<Control>().Where(control => control.IsVisible && control.Bounds.Width > 0 && (control is TextBox or ToggleSwitch or ListBoxItem or Button)))
        {
            Point? origin = control.TranslatePoint(default, window);
            if (origin is not { } topLeft)
            {
                continue;
            }

            Rect box = new(topLeft, control.Bounds.Size);
            bool footer = control is Button { Classes: var classes } && classes.Contains("footer");
            if (box.Right > client.Right + 0.5 || box.Left < -0.5 || (footer && (box.Bottom > client.Bottom + 0.5 || box.Top < -0.5)))
            {
                problems.Add($"{control.GetType().Name} {AutomationName(control)} at {box}");
            }
        }

        return problems;
    }

    private static string AutomationName(Control control) => Avalonia.Automation.AutomationProperties.GetName(control) ?? string.Empty;
}
