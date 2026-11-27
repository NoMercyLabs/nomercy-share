using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoMercy.Share.App;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

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

        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        BarWindow window = Open(store);
        Save(window, Path.Combine(folder, "share-bar-default.png"));
        Button[] buttons = [.. window.GetVisualDescendants().OfType<Button>()];
        ((IPseudoClasses)buttons[3].Classes).Set(":pointerover", true);
        Save(window, Path.Combine(folder, "share-bar-hover.png"));
        ((IPseudoClasses)buttons[3].Classes).Set(":pointerover", false);
        buttons[0].Focus(NavigationMethod.Tab);
        Save(window, Path.Combine(folder, "share-bar-focus.png"));
        ((BarViewModel)window.DataContext!).RegionCommand.Execute(null);
        Save(window, Path.Combine(folder, "share-bar-notice.png"));

        window.Position = new PixelPoint(300, 200);
        window.Close();
        BarWindow reopened = Open(store);
        Console.WriteLine($"saved position {store.Load()}; reopened at {reopened.Position}");
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        Save(reopened, Path.Combine(folder, "share-bar-light.png"));
        reopened.Close();
        return store.Load() == new BarPosition(300, 200) ? 0 : 1;
    }

    private static BarWindow Open(BarPositionStore store)
    {
        ShareFlow flow = new(new NoUpload(), new NoClipboard(), new ExpirySettings());
        BarViewModel model = new(new NotBuiltCapture(), new NotBuiltRecorder(), new NotBuiltWeb(), flow);
        BarWindow window = new(model, store, new BarSettings());
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Save(Window window, string file)
    {
        Dispatcher.UIThread.RunJobs();
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        frame?.Save(file);
        Console.WriteLine(file);
    }

    private sealed class NotBuiltCapture : IScreenCapture
    {
        public Task<CaptureResult> CaptureRegionAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<CaptureResult> CaptureWindowAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<CaptureResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NotBuiltRecorder : IScreenRecorder
    {
        public bool IsRecording => false;

        public Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<CaptureResult> StopAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NotBuiltWeb : IWebPageCapture
    {
        public Task<CaptureResult> CaptureAsync(WebCaptureRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NoUpload : IShareUploader
    {
        public string ProviderName => "r2";

        public Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NoClipboard : NoMercy.Share.Core.IClipboard
    {
        public Task SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
