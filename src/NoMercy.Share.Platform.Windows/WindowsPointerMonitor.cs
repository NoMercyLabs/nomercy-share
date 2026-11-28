using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

public sealed class WindowsPointerMonitor : IPointerMonitor
{
    public ScreenRect Bounds()
    {
        using DpiScope scope = DpiScope.PerMonitor();
        return GdiScreen.CursorMonitor();
    }
}
