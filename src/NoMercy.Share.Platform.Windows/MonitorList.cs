using System.Runtime.InteropServices;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

/// <summary>The monitors in the order Windows lists them, which is the order Desktop Duplication numbers its outputs.</summary>
internal static unsafe class MonitorList
{
    /// <summary>Every monitor as a rectangle of the virtual screen. Call it inside a <see cref="DpiScope"/> so the numbers are physical pixels.</summary>
    public static IReadOnlyList<ScreenRect> Current()
    {
        List<ScreenRect> monitors = [];
        GCHandle handle = GCHandle.Alloc(monitors);
        try
        {
            NativeMethods.EnumDisplayMonitors(0, 0, &Collect, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return monitors;
    }

    [UnmanagedCallersOnly]
    private static int Collect(nint monitor, nint dc, NativeMethods.Rect* area, nint data)
    {
        List<ScreenRect> monitors = (List<ScreenRect>)GCHandle.FromIntPtr(data).Target!;
        monitors.Add(new ScreenRect(area->Left, area->Top, area->Right - area->Left, area->Bottom - area->Top));
        return 1;
    }
}
