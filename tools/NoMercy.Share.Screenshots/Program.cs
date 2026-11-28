using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoMercy.Share.App;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;
using NoMercy.Share.Core.Tests;

namespace NoMercy.Share.Screenshots;

/// <summary>"&lt;folder&gt;": renders the bar in its states to share-bar-*.png and proves the position is remembered.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string folder = args.Length > 0 ? args[0] : "share-shots";
        Directory.CreateDirectory(folder);
        AppBuilder.Configure<NoMercy.Share.App.App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        string settings = Path.Combine(Path.GetTempPath(), $"share-shots-{Guid.NewGuid():N}", "settings.json");
        BarPositionStore store = new(new SettingsFile(settings));

        RenderBar(folder, store);
        bool remembered = CheckPositionRemembered(folder, store);
        RenderRegionOverlay(folder);
        bool popupFollows = RenderRecent(folder);
        RenderSettings(folder);
        RenderWebCapture(folder);
        return remembered && popupFollows ? 0 : 1;
    }

    /// <summary>The settings window at 2x, dark and light, with made-up values; and once more with a bad endpoint, an empty bucket and a bad chord.</summary>
    private static void RenderSettings(string folder)
    {
        foreach ((ThemeVariant theme, string name) in new[] { (ThemeVariant.Dark, "dark"), (ThemeVariant.Light, "light") })
        {
            Application.Current!.RequestedThemeVariant = theme;
            SettingsViewModel model = SettingsModel(Path.Combine(Path.GetTempPath(), $"share-settings-shots-{Guid.NewGuid():N}"));
            foreach (SettingsSection section in Enum.GetValues<SettingsSection>())
            {
                SaveSettings(Path.Combine(folder, $"share-settings-look-{section.ToString().ToLowerInvariant()}-{name}.png"), model, section);
            }

            SaveSettings(Path.Combine(folder, $"share-settings-look-hotkeys-record-{name}.png"), model, SettingsSection.Hotkeys, recordFirstChord: true);
            SettingsViewModel sound = SettingsModel(Path.Combine(Path.GetTempPath(), $"share-settings-shots-{Guid.NewGuid():N}"));
            sound.RecordSystemSound = true;
            sound.RecordMicrophone = true;
            SaveSettings(Path.Combine(folder, $"share-settings-audio-{name}.png"), sound, SettingsSection.Recording);
            SaveSettings(Path.Combine(folder, $"share-settings-focus-rail-{name}.png"), model, SettingsSection.Sharing, prepare: root => root.GetVisualDescendants().OfType<ListBoxItem>().First().Focus(NavigationMethod.Tab));
            SaveSettings(Path.Combine(folder, $"share-settings-focus-segmented-{name}.png"), model, SettingsSection.Sharing, prepare: root =>
                root.GetVisualDescendants().OfType<ListBox>().First(list => list.Classes.Contains("segmented")).GetVisualDescendants().OfType<ListBoxItem>().First().Focus(NavigationMethod.Tab));
            SaveSettings(Path.Combine(folder, $"share-settings-focus-switch-{name}.png"), model, SettingsSection.Recording, prepare: root => root.GetVisualDescendants().OfType<ToggleSwitch>().First().Focus(NavigationMethod.Tab));
            SaveSettings(Path.Combine(folder, $"share-settings-combo-open-{name}.png"), model, SettingsSection.Links, prepare: root => root.GetVisualDescendants().OfType<ComboBox>().First().IsDropDownOpen = true);
            SaveSettings(Path.Combine(folder, $"share-settings-scrolled-end-{name}.png"), model, SettingsSection.Sharing, prepare: root => root.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(viewer => viewer.Extent.Height > viewer.Viewport.Height)?.ScrollToEnd());
            SettingsViewModel failing = SettingsModel(Path.Combine(Path.GetTempPath(), $"share-settings-shots-{Guid.NewGuid():N}"));
            typeof(SettingsViewModel).GetProperty(nameof(SettingsViewModel.SaveError))!.SetValue(failing, "The settings were not saved: access to the path is denied.");
            SaveSettings(Path.Combine(folder, $"share-settings-save-error-{name}.png"), failing, SettingsSection.Hotkeys);
            SettingsViewModel discordLinks = SettingsModel(Path.Combine(Path.GetTempPath(), $"share-settings-shots-{Guid.NewGuid():N}"), DiscordSettings.ProviderName, webhookStored: true);
            SaveSettings(Path.Combine(folder, $"share-settings-discord-links-{name}.png"), discordLinks, SettingsSection.Links);
            SettingsViewModel unset = SettingsModel(Path.Combine(Path.GetTempPath(), $"share-settings-shots-{Guid.NewGuid():N}"), unset: true);
            SaveSettings(Path.Combine(folder, $"share-settings-notsetup-{name}.png"), unset);
            model.Endpoint = "not an address";
            model.Bucket = "";
            model.Hotkeys[2].Chord = "Ctrl+Nonsense";
            model.MaxSizeMegabytes = "abc";
            model.Save();
            SaveSettings(Path.Combine(folder, $"share-settings-errors-{name}.png"), model, SettingsSection.Sharing);
            SaveSettings(Path.Combine(folder, $"share-settings-errors-recording-{name}.png"), model, SettingsSection.Recording);

            SettingsViewModel on = SettingsModel(Path.Combine(Path.GetTempPath(), $"share-settings-shots-{Guid.NewGuid():N}"), DiscordSettings.ProviderName, webhookStored: true);
            SaveSettings(Path.Combine(folder, $"share-settings-discordprovider-{name}.png"), on);
            SettingsViewModel wrong = SettingsModel(Path.Combine(Path.GetTempPath(), $"share-settings-shots-{Guid.NewGuid():N}"), DiscordSettings.ProviderName);
            wrong.DiscordWebhook = "https://example.test/not-discord";
            wrong.Save();
            SaveSettings(Path.Combine(folder, $"share-settings-discordprovider-error-{name}.png"), wrong);
        }
    }

    /// <summary>The web capture window at 2x, dark and light: Full page with a made-up address, and One element with a bad address and no selector.</summary>
    private static void RenderWebCapture(string folder)
    {
        foreach ((ThemeVariant theme, string name) in new[] { (ThemeVariant.Dark, "dark"), (ThemeVariant.Light, "light") })
        {
            Application.Current!.RequestedThemeVariant = theme;
            WebCaptureViewModel model = new(new FakeWebPageCapture(), new WebCaptureSettings(), "https://example.test/articles/made-up");
            SaveWebCapture(Path.Combine(folder, $"share-web-{name}.png"), model);
            model.SelectedMode = model.Modes.Single(mode => mode.Kind == WebCaptureKind.Element);
            model.Address = "example.test";
            model.CaptureAsync().GetAwaiter().GetResult();
            SaveWebCapture(Path.Combine(folder, $"share-web-element-errors-{name}.png"), model);
        }
    }

    private static void SaveWebCapture(string file, WebCaptureViewModel model)
    {
        WebCaptureWindow shown = new(model);
        Control content = (Control)shown.Content!;
        shown.Content = null;
        content.Width = shown.Width;
        LayoutTransformControl scaled = new() { LayoutTransform = new ScaleTransform(2, 2), Child = content };
        Window window = new() { Classes = { "settings" }, DataContext = model, Content = scaled, SizeToContent = SizeToContent.WidthAndHeight, CanResize = false };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        using RenderTargetBitmap bitmap = Render(window, 1, 0, window.Bounds.Width);
        bitmap.Save(file);
        Console.WriteLine($"{file} {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
        window.Close();
    }

    private static SettingsViewModel SettingsModel(string temp, string provider = "r2", bool webhookStored = false, bool unset = false)
    {
        SettingsFile file = new(Path.Combine(temp, "settings.json"));
        file.Save(new ShareSettings
        {
            Storage = unset
                ? new StorageSettings { Provider = provider }
                : new StorageSettings
                {
                    Provider = provider,
                    Endpoint = "https://storage.example.test",
                    Bucket = "made-up-bucket",
                    PublicBaseUrl = "https://share.example.test",
                    CredentialName = "made-up"
                },
            Expiry = new ExpirySettings { DefaultDays = 7 }
        });
        FakeCredentialStore credentials = new();
        if (!unset)
        {
            credentials.Stored["made-up"] = new StoredCredential("made-up-key-id", "made-up-secret");
        }

        if (webhookStored)
        {
            credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", "https://discord.com/api/webhooks/000/made-up-token");
        }

        BarViewModel bar = new(new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings()),
            new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()));
        return new SettingsViewModel(file, new LiveSettings(file.Load()), credentials, new HotkeyBinder(new FakeGlobalHotkeys(), bar, new HotkeySettings()), ["r2", "s3", "discord"]);
    }

    private static void SaveSettings(string file, SettingsViewModel model, SettingsSection section = SettingsSection.Sharing, bool recordFirstChord = false, Action<Control>? prepare = null)
    {
        // The window's own content goes into a host so the 2x is a layout transform, as for the recent list.
        model.SelectedSection = section;
        SettingsWindow settings = new(model);
        Control content = (Control)settings.Content!;
        settings.Content = null;
        content.Width = settings.Width;
        content.Height = settings.Height;
        LayoutTransformControl scaled = new() { LayoutTransform = new ScaleTransform(2, 2), Child = content };
        Window window = new() { Classes = { "settings" }, DataContext = model, Content = scaled, Width = settings.Width * 2, Height = settings.Height * 2, CanResize = false };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        if (prepare is not null)
        {
            prepare(content);
            Dispatcher.UIThread.RunJobs();
        }

        if (recordFirstChord)
        {
            content.GetVisualDescendants().OfType<TextBox>().First(box => box.Classes.Contains("chord")).Focus();
            Dispatcher.UIThread.RunJobs();
        }

        using RenderTargetBitmap bitmap = Render(window, 1, 0, window.Bounds.Width);
        bitmap.Save(file);
        Console.WriteLine($"{file} {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
        window.Close();
    }

    /// <summary>The recent links list, dark and light, with three made-up links and empty, at 2x; also checks the bar's popup opens and closes with the list.</summary>
    private static bool RenderRecent(string folder)
    {
        DateTimeOffset now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        string temp = Path.Combine(Path.GetTempPath(), $"share-recent-shots-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        string picture = Path.Combine(temp, "capture.png");
        File.WriteAllBytes(picture, PngEncoder.Encode(SampleScreen(320, 200)));
        foreach ((ThemeVariant theme, string name, string desktop) in new[] { (ThemeVariant.Dark, "dark", "#3A3F4B"), (ThemeVariant.Light, "light", "#C9CED8") })
        {
            Application.Current!.RequestedThemeVariant = theme;
            RecentLinks full = new(Path.Combine(temp, $"full-{name}.json"), new RecentSettings(), () => now);
            full.Add(new RecentLink("https://share.example.test/exp-30d/Qx7LmP2aRt.png", RecentKind.Image, "image/png", 48_000, now.AddMinutes(-5), now.AddDays(29), picture));
            full.Add(new RecentLink("https://share.example.test/exp-7d/Zk3VbN8cYd.mp4", RecentKind.Video, "video/mp4", 9_000_000, now.AddHours(-3), now.AddDays(4), null));
            full.Add(new RecentLink("https://share.example.test/exp-30d/Hh5TgW1eUo.png", RecentKind.Image, "image/png", 52_000, now.AddDays(-31), now.AddDays(-1), null));
            SaveRecent(Path.Combine(folder, $"share-recent-{name}.png"), full, now, desktop);
            SaveRecent(Path.Combine(folder, $"share-recent-empty-{name}.png"), new RecentLinks(Path.Combine(temp, $"none-{name}.json"), new RecentSettings(), () => now), now, desktop);
        }

        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        BarWindow window = Open(new BarPositionStore(new SettingsFile(Path.Combine(temp, "settings.json"))));
        BarViewModel bar = (BarViewModel)window.DataContext!;
        Popup popup = window.FindControl<Popup>("RecentPopup")!;
        bar.IsRecentOpen = true;
        bool opened = popup.IsOpen;
        bar.CloseRecent();
        Console.WriteLine($"recent popup follows the list: open={opened}, after close={popup.IsOpen}");
        window.Close();
        return opened && !popup.IsOpen && CheckPanelKeys(temp, now);
    }

    /// <summary>Enter on the selected row copies its link and Escape closes the list.</summary>
    private static bool CheckPanelKeys(string temp, DateTimeOffset now)
    {
        FakeClipboard clipboard = new();
        RecentLinks store = new(Path.Combine(temp, "keys.json"), new RecentSettings(), () => now);
        store.Add(new RecentLink("https://share.example.test/exp-30d/Qx7LmP2aRt.png", RecentKind.Image, "image/png", 48_000, now, now.AddDays(29), null));
        CaptureFlow capture = new(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 });
        BarViewModel model = new(capture, new ShareFlow(new FakeUploader(), clipboard, new ExpirySettings(), new ClipboardSettings()), clock: () => now, recent: store, opener: new FakeUrlOpener(), clipboard: clipboard);
        model.RunAsync(BarAction.Recent).GetAwaiter().GetResult();
        RecentPanel panel = new() { DataContext = model };
        Window host = new() { Content = panel };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        panel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        bool copied = clipboard.Texts.Count == 1 && model.Notice == "Link copied";
        model.IsRecentOpen = true;
        panel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Console.WriteLine($"recent keys: enter copied={copied}, escape closed={!model.IsRecentOpen}");
        host.Close();
        return copied && !model.IsRecentOpen;
    }

    private static void SaveRecent(string file, RecentLinks store, DateTimeOffset now, string desktop)
    {
        CaptureFlow capture = new(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 });
        ShareFlow flow = new(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings());
        BarViewModel model = new(capture, flow, clock: () => now, recent: store, opener: new FakeUrlOpener(), clipboard: new FakeClipboard());
        model.RunAsync(BarAction.Recent).GetAwaiter().GetResult();
        // The 2x is a layout transform, so the window grows to the scaled size and nothing is clipped at the 1x bounds.
        LayoutTransformControl scaled = new() { LayoutTransform = new ScaleTransform(2, 2), Child = new RecentPanel { DataContext = model } };
        Window host = new() { Content = scaled, SizeToContent = SizeToContent.WidthAndHeight, Background = new SolidColorBrush(Color.Parse(desktop)) };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        using RenderTargetBitmap bitmap = Render(host, 1, 0, host.Bounds.Width);
        bitmap.Save(file);
        Console.WriteLine($"{file} {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height} rows={model.RecentRows.Count}");
        host.Close();
    }

    /// <summary>The bar in the dark theme: default, zoomed ends, hover, focus and a notice.</summary>
    private static void RenderBar(string folder, BarPositionStore store)
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        BarWindow window = Open(store);
        Save(window, Path.Combine(folder, "share-bar-default.png"));
        window.CaptureRenderedFrame()?.Save(Path.Combine(folder, "share-bar-frame.png"));
        SaveCrop(window, Path.Combine(folder, "share-bar-zoom-left.png"), 0, 64);
        SaveCrop(window, Path.Combine(folder, "share-bar-zoom-right.png"), window.Bounds.Width - 64, 64);
        IPseudoClasses recordClasses = Named<Button>(window, "RecordButton").Classes;
        recordClasses.Set(":pointerover", true);
        Save(window, Path.Combine(folder, "share-bar-hover.png"));
        recordClasses.Set(":pointerover", false);
        Named<Button>(window, "ScreenshotButton").Focus(NavigationMethod.Tab);
        Save(window, Path.Combine(folder, "share-bar-focus.png"));
        ((BarViewModel)window.DataContext!).ShowMessage("Link copied");
        Save(window, Path.Combine(folder, "share-bar-notice.png"));
        window.Close();
    }

    /// <summary>Moves a bar, closes it, opens another and checks it comes back where it was left; also renders the light theme.</summary>
    private static bool CheckPositionRemembered(string folder, BarPositionStore store)
    {
        BarWindow window = Open(store);
        window.Position = new PixelPoint(300, 200);
        window.Close();
        BarWindow reopened = Open(store);
        Console.WriteLine($"saved position {store.Load()}; reopened at {reopened.Position}");
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        Save(reopened, Path.Combine(folder, "share-bar-light.png"));
        reopened.Close();
        return store.Load() == new BarPosition(300, 200);
    }

    /// <summary>The region overlay on a made-up frozen screen with a selection drawn: full size, and a zoom on both ends of the selection.</summary>
    private static void RenderRegionOverlay(string folder)
    {
        const int width = 1280;
        const int height = 720;
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        RegionOverlayWindow overlay = new(SampleScreen(width, height), 1, new NoForegroundGrant());
        overlay.Show();
        overlay.SetSelection(new Point(320, 180), new Point(900, 520));
        Dispatcher.UIThread.RunJobs();
        using (RenderTargetBitmap full = Render(overlay, 1, 0, width, 0, height))
        {
            full.Save(Path.Combine(folder, "region-overlay.png"));
        }

        using (RenderTargetBitmap start = Render(overlay, 4, 280, 120, 140, 90))
        {
            start.Save(Path.Combine(folder, "region-overlay-zoom-start.png"));
        }

        using (RenderTargetBitmap end = Render(overlay, 4, 820, 140, 460, 130))
        {
            end.Save(Path.Combine(folder, "region-overlay-zoom-end.png"));
        }

        overlay.Close();
    }

    /// <summary>A made-up screen: a diagonal gradient, a grid and a few colored blocks, so dimming and edges are easy to judge.</summary>
    private static ScreenFrame SampleScreen(int width, int height)
    {
        byte[] pixels = new byte[width * height * ScreenFrame.BytesPerPixel];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int at = ((y * width) + x) * ScreenFrame.BytesPerPixel;
                bool grid = x % 80 == 0 || y % 80 == 0;
                pixels[at] = (byte)(grid ? 220 : 60 + (x * 120 / width));
                pixels[at + 1] = (byte)(grid ? 220 : 50 + (y * 120 / height));
                pixels[at + 2] = (byte)(grid ? 220 : 90);
                pixels[at + 3] = 255;
            }
        }

        return new ScreenFrame(0, 0, width, height, pixels);
    }

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static BarWindow Open(BarPositionStore store)
    {
        ShareFlow flow = new(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings());
        CaptureFlow capture = new(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 });
        BarViewModel model = new(capture, flow, new FakeScreenRecorder(), new FakeWebPageCapture());
        BarWindow window = new(model, store, new BarSettings());
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>Renders the window at 1x with its alpha kept (the area outside the pill stays transparent).</summary>
    private static void Save(Window window, string file)
    {
        Dispatcher.UIThread.RunJobs();
        using RenderTargetBitmap bitmap = Render(window, 1, 0, window.Bounds.Width);
        bitmap.Save(file);
        Console.WriteLine($"{file} corner alpha={CornerAlpha(bitmap)}");
    }

    /// <summary>A zoomed crop of one end of the bar: <paramref name="from"/> and <paramref name="width"/> are in device-independent units.</summary>
    private static void SaveCrop(Window window, string file, double from, double width)
    {
        using RenderTargetBitmap bitmap = Render(window, 8, from, width);
        bitmap.Save(file);
        Console.WriteLine(file);
    }

    private static RenderTargetBitmap Render(Window window, double scale, double from, double width, double fromY = 0, double? height = null)
    {
        Control root = (Control)window.GetVisualChildren().First();
        // A bitmap with a dpi above 96 puts the children in the wrong place in headless Skia, so the scale is a transform on a 96-dpi bitmap.
        root.RenderTransform = new TransformGroup { Children = { new TranslateTransform(-from, -fromY), new ScaleTransform(scale, scale) } };
        root.RenderTransformOrigin = RelativePoint.TopLeft;
        RenderTargetBitmap bitmap = new(new PixelSize((int)(width * scale), (int)((height ?? window.Bounds.Height) * scale)));
        bitmap.Render(root);
        root.RenderTransform = null;
        return bitmap;
    }

    private static byte CornerAlpha(RenderTargetBitmap bitmap)
    {
        IntPtr pixel = Marshal.AllocHGlobal(4);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, 1, 1), pixel, 4, 4);
            return Marshal.ReadByte(pixel, 3);
        }
        finally
        {
            Marshal.FreeHGlobal(pixel);
        }
    }
}
