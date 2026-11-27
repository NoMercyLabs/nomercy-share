using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>
/// Shows the frozen screen as one overlay per monitor, so every monitor keeps its own display scale. A selection stays on the monitor
/// it started on. It lives in the app because it needs this UI toolkit.
/// </summary>
public sealed class AvaloniaRegionSelector(Func<TopLevel?> owner, IForegroundGrant foreground) : IRegionSelector
{
    public async Task<ScreenRect?> SelectAsync(ScreenFrame frozen, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<ScreenRect?> done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<RegionOverlayWindow> overlays = [];
        await Dispatcher.UIThread.InvokeAsync(() => Open(frozen, overlays, done));
        await using CancellationTokenRegistration registration = cancellationToken.Register(() => done.TrySetResult(null));
        try
        {
            return await done.Task;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => overlays.ForEach(window => window.Close()));
        }
    }

    private void Open(ScreenFrame frozen, List<RegionOverlayWindow> overlays, TaskCompletionSource<ScreenRect?> done)
    {
        IReadOnlyList<Screen> screens = owner()?.Screens?.All ?? [];
        foreach (Screen screen in screens)
        {
            PixelRect monitor = screen.Bounds;
            ScreenRect area = frozen.Bounds.Intersect(new ScreenRect(monitor.X, monitor.Y, monitor.Width, monitor.Height));
            if (area.IsEmpty)
            {
                continue;
            }

            RegionOverlayWindow overlay = new(frozen.Crop(area), screen.Scaling, foreground);
            overlay.Confirmed += (_, selection) => done.TrySetResult(selection);
            overlay.Cancelled += (_, _) => done.TrySetResult(null);
            overlays.Add(overlay);
        }

        if (overlays.Count == 0)
        {
            done.TrySetResult(null);
            return;
        }

        overlays.ForEach(window => window.Show());
    }
}
