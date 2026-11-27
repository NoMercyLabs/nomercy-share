using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>
/// Shows the frozen screen as one overlay per monitor, so every monitor keeps its own display scale. A selection stays on the monitor
/// it started on. It lives in the app because it needs this UI toolkit.
/// </summary>
public sealed class AvaloniaRegionSelector(Func<TopLevel?> owner) : IRegionSelector
{
    public async Task<PixelRect?> SelectAsync(ScreenFrame frozen, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<PixelRect?> done = new(TaskCreationOptions.RunContinuationsAsynchronously);
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

    private void Open(ScreenFrame frozen, List<RegionOverlayWindow> overlays, TaskCompletionSource<PixelRect?> done)
    {
        IReadOnlyList<Screen> screens = owner()?.Screens?.All ?? [];
        foreach (Screen screen in screens)
        {
            PixelRect area = Intersect(frozen.Bounds, new PixelRect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height));
            if (area.IsEmpty)
            {
                continue;
            }

            RegionOverlayWindow overlay = new(frozen.Crop(area), screen.Scaling);
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

    private static PixelRect Intersect(PixelRect a, PixelRect b)
    {
        int left = Math.Max(a.X, b.X);
        int top = Math.Max(a.Y, b.Y);
        return new PixelRect(left, top, Math.Min(a.Right, b.Right) - left, Math.Min(a.Bottom, b.Bottom) - top);
    }
}
