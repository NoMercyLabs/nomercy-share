using NoMercy.Share.Core;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// Windows refuses SetForegroundWindow to a process that did not receive the last input, and a low-level keyboard hook does not count.
/// Attaching this thread's input to the foreground window's thread for the one call lets it through; the attachment is always undone.
/// </summary>
public sealed class WindowsForegroundGrant : IForegroundGrant
{
    public void BringToForeground(nint window)
    {
        if (window == 0 || NativeMethods.GetForegroundWindow() == window)
        {
            return;
        }

        nint foreground = NativeMethods.GetForegroundWindow();
        uint foregroundThread = foreground == 0 ? 0 : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        uint ownThread = NativeMethods.GetCurrentThreadId();
        bool attached = foregroundThread != 0 && foregroundThread != ownThread && NativeMethods.AttachThreadInput(ownThread, foregroundThread, true);
        try
        {
            NativeMethods.BringWindowToTop(window);
            NativeMethods.SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(ownThread, foregroundThread, false);
            }
        }
    }
}
