using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: DisableRuntimeMarshalling]

namespace NoMercy.Share.Platform.Windows;

/// <summary>The Win32 calls the capture needs: GDI for pixels, user32 for windows and monitors, DWM for the true window frame.</summary>
internal static partial class NativeMethods
{
    /// <summary>Copy the source as it is, including layered (transparent) windows.</summary>
    internal const uint SrcCopyCaptureBlt = 0x40CC0020;

    /// <summary>PrintWindow flag: render the full content, also of windows drawn by DirectComposition.</summary>
    internal const uint PrintWindowRenderFullContent = 2;

    internal const int DwmExtendedFrameBounds = 9;

    internal const uint GetAncestorRoot = 2;

    internal const uint MonitorDefaultToNearest = 2;

    internal const int VirtualScreenLeft = 76;

    internal const int VirtualScreenTop = 77;

    internal const int VirtualScreenWidth = 78;

    internal const int VirtualScreenHeight = 79;

    /// <summary>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2: pixel coordinates are physical on every monitor.</summary>
    internal static readonly nint PerMonitorAwareV2 = -4;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [LibraryImport("user32.dll")]
    internal static partial nint SetThreadDpiAwarenessContext(nint context);

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    internal static partial nint GetDC(nint window);

    [LibraryImport("user32.dll")]
    internal static partial int ReleaseDC(nint window, nint dc);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromPoint(Point point, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool EnumDisplayMonitors(nint dc, nint clip, delegate* unmanaged<nint, nint, Rect*, nint, int> callback, nint data);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    internal static partial nint WindowFromPoint(Point point);

    [LibraryImport("user32.dll")]
    internal static partial nint GetAncestor(nint window, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint window, out Rect rect);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    internal static partial int GetClassName(nint window, ref char buffer, int maxCount);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PrintWindow(nint window, nint dc, uint flags);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmGetWindowAttribute(nint window, int attribute, out Rect value, int size);

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateDIBSection(nint dc, in BitmapInfoHeader info, uint usage, out nint bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    internal static partial nint SelectObject(nint dc, nint item);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint item);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(nint dc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
}
