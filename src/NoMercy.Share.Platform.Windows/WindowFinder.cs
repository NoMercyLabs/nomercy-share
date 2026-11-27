using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Finds the window a window capture should take, and its true visible frame.</summary>
internal static class WindowFinder
{
    /// <summary>Windows of the desktop shell: the wallpaper host and the taskbars. They are never a capture target.</summary>
    private static readonly HashSet<string> ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    /// <summary>
    /// The window that was active before the bar: with the bar hidden, Windows makes the previous window the foreground one.
    /// When that is not a usable window, the window under the cursor (its top-level owner) is the second choice.
    /// </summary>
    public static nint FindTarget()
    {
        nint foreground = NativeMethods.GetForegroundWindow();
        if (IsCandidate(foreground))
        {
            return foreground;
        }

        NativeMethods.GetCursorPos(out NativeMethods.Point cursor);
        nint underCursor = NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(cursor), NativeMethods.GetAncestorRoot);
        return IsCandidate(underCursor) ? underCursor : throw new InvalidOperationException("There is no window to capture.");
    }

    /// <summary>The window rectangle with its invisible resize border, in physical pixels.</summary>
    public static ScreenRect OuterBounds(nint window)
    {
        if (!NativeMethods.GetWindowRect(window, out NativeMethods.Rect rect))
        {
            throw new InvalidOperationException("Windows did not give the window's size.");
        }

        return ToScreenRect(rect);
    }

    /// <summary>The visible frame from DWM, without the invisible border and shadow area; the outer bounds when DWM has none.</summary>
    public static ScreenRect VisibleBounds(nint window, ScreenRect outer)
    {
        int result = NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DwmExtendedFrameBounds, out NativeMethods.Rect frame,
            System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Rect>());
        return result == 0 ? ToScreenRect(frame) : outer;
    }

    private static bool IsCandidate(nint window)
    {
        if (window == 0 || !NativeMethods.IsWindowVisible(window) || NativeMethods.IsIconic(window))
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(window, out uint processId);
        return processId != Environment.ProcessId && !ShellClasses.Contains(ClassName(window));
    }

    private static string ClassName(nint window)
    {
        Span<char> buffer = stackalloc char[256];
        int length = NativeMethods.GetClassName(window, ref buffer[0], buffer.Length);
        return new string(buffer[..Math.Max(0, length)]);
    }

    private static ScreenRect ToScreenRect(NativeMethods.Rect rect) => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
}
