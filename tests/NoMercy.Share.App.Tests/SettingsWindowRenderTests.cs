using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
    [InlineData(SettingsSection.Sharing)]
    [InlineData(SettingsSection.Links)]
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
        SettingsViewModel model = new(file, new LiveSettings(file.Load()), new FakeCredentialStore(), new HotkeyBinder(new FakeGlobalHotkeys(), bar, new HotkeySettings()), ["r2", "s3", "discord"])
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
