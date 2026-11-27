using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoMercy.Share.App;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
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
        return remembered ? 0 : 1;
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
        Named<Button>(window, "RegionButton").Focus(NavigationMethod.Tab);
        Save(window, Path.Combine(folder, "share-bar-focus.png"));
        ((BarViewModel)window.DataContext!).ActionCommand.Execute(BarAction.Recent);
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
        RegionOverlayWindow overlay = new(SampleScreen(width, height), 1);
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
