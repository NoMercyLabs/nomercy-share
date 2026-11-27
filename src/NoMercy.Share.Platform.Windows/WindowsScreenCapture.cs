using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Still pictures on Windows in physical pixels: BitBlt for full screen and region, PrintWindow cropped to the DWM frame for a window.</summary>
public sealed class WindowsScreenCapture(CaptureFiles files, IRegionSelector selector, CaptureSettings settings) : IScreenCapture
{
    public async Task<CaptureResult> CaptureRegionAsync(CancellationToken cancellationToken = default)
    {
        ScreenFrame frozen = await Task.Run(() => Frozen(GdiScreen.VirtualScreen), cancellationToken);
        ScreenRect? chosen = await selector.SelectAsync(frozen, cancellationToken);
        if (chosen is not { IsEmpty: false } area)
        {
            throw new OperationCanceledException("The region selection was cancelled.");
        }

        return await SaveAsync(frozen.Crop(area), cancellationToken);
    }

    public Task<CaptureResult> CaptureWindowAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => SaveAsync(DpiAware(WindowFrame), cancellationToken), cancellationToken);

    public Task<CaptureResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => SaveAsync(Frozen(FullScreenRect), cancellationToken), cancellationToken);

    /// <summary>Runs one capture step with this thread per-monitor DPI aware.</summary>
    private static ScreenFrame DpiAware(Func<ScreenFrame> take)
    {
        using DpiScope scope = DpiScope.PerMonitor();
        return take();
    }

    private static ScreenFrame Frozen(Func<ScreenRect> area) => DpiAware(() => GdiScreen.Capture(area()));

    private ScreenRect FullScreenRect() =>
        settings.FullScreenArea == FullScreenArea.VirtualScreen ? GdiScreen.VirtualScreen() : GdiScreen.CursorMonitor();

    private static ScreenFrame WindowFrame()
    {
        nint window = WindowFinder.FindTarget();
        ScreenRect outer = WindowFinder.OuterBounds(window);
        ScreenRect visible = WindowFinder.VisibleBounds(window, outer);
        using Dib dib = Dib.Create(outer.Width, outer.Height);
        if (!NativeMethods.PrintWindow(window, dib.Context, NativeMethods.PrintWindowRenderFullContent))
        {
            return GdiScreen.Capture(visible);
        }

        return new ScreenFrame(outer.X, outer.Y, outer.Width, outer.Height, dib.ReadOpaque()).Crop(visible.Intersect(outer));
    }

    private async Task<CaptureResult> SaveAsync(ScreenFrame frame, CancellationToken cancellationToken)
    {
        string path = files.NewPath();
        byte[] png = await Task.Run(() => PngEncoder.Encode(frame), cancellationToken);
        await File.WriteAllBytesAsync(path, png, cancellationToken);
        return new CaptureResult(path, "image/png", frame.Width, frame.Height);
    }
}
