using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>The toolkit's screen for the monitor the mouse is on; null when the platform cannot say.</summary>
internal static class PointerScreen
{
    /// <summary>Opens a window in the middle of the screen the mouse is on; without one it opens as its own markup says.</summary>
    public static void CenterOnOpen(Window window, IPointerMonitor? pointer)
    {
        if (pointer is null)
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Opened += (_, _) =>
        {
            if (Of(window.Screens, pointer) is not { } screen)
            {
                return;
            }

            PixelSize size = PixelSize.FromSize(window.FrameSize ?? window.ClientSize, window.RenderScaling);
            window.Position = screen.WorkingArea.CenterRect(new PixelRect(size)).Position;
        };
    }

    public static Screen? Of(Screens screens, IPointerMonitor? pointer)
    {
        if (pointer is null)
        {
            return null;
        }

        ScreenRect bounds = pointer.Bounds();
        return screens.ScreenFromPoint(new PixelPoint(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2)));
    }
}
