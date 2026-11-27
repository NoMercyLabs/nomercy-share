using System.Runtime.InteropServices;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// A 32-bit device-independent bitmap with a drawing context, so pixels can be copied in (BitBlt, PrintWindow) and
/// read straight from memory.
/// </summary>
internal sealed class Dib : IDisposable
{
    private readonly nint _screen;
    private readonly nint _bitmap;
    private readonly nint _previous;
    private readonly nint _bits;

    private Dib(int width, int height)
    {
        Width = width;
        Height = height;
        _screen = NativeMethods.GetDC(0);
        Context = NativeMethods.CreateCompatibleDC(_screen);
        NativeMethods.BitmapInfoHeader info = new()
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
            Width = width,
            Height = -height, // negative: rows run top to bottom
            Planes = 1,
            BitCount = 32
        };
        _bitmap = NativeMethods.CreateDIBSection(_screen, in info, 0, out _bits, 0, 0);
        if (_bitmap == 0)
        {
            Dispose();
            throw new InvalidOperationException($"Windows could not allocate a {width} x {height} picture.");
        }

        _previous = NativeMethods.SelectObject(Context, _bitmap);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The device context that draws into this bitmap.</summary>
    public nint Context { get; }

    public static Dib Create(int width, int height) => new(width, height);

    /// <summary>The pixels as blue, green, red, alpha bytes; the screen is opaque, so alpha is set to 255.</summary>
    public byte[] ReadOpaque()
    {
        byte[] bytes = new byte[Width * Height * ScreenFrame.BytesPerPixel];
        Marshal.Copy(_bits, bytes, 0, bytes.Length);
        for (int alpha = 3; alpha < bytes.Length; alpha += ScreenFrame.BytesPerPixel)
        {
            bytes[alpha] = 255;
        }

        return bytes;
    }

    public void Dispose()
    {
        if (_previous != 0)
        {
            NativeMethods.SelectObject(Context, _previous);
        }

        if (_bitmap != 0)
        {
            NativeMethods.DeleteObject(_bitmap);
        }

        if (Context != 0)
        {
            NativeMethods.DeleteDC(Context);
        }

        if (_screen != 0)
        {
            NativeMethods.ReleaseDC(0, _screen);
        }
    }
}

/// <summary>Takes pixels from the screen with GDI. Call it inside a <see cref="DpiScope"/> so every coordinate is a physical pixel.</summary>
internal static class GdiScreen
{
    /// <summary>All monitors together; X and Y are negative when a monitor sits left of or above the main one.</summary>
    public static PixelRect VirtualScreen() => new(
        NativeMethods.GetSystemMetrics(NativeMethods.VirtualScreenLeft),
        NativeMethods.GetSystemMetrics(NativeMethods.VirtualScreenTop),
        NativeMethods.GetSystemMetrics(NativeMethods.VirtualScreenWidth),
        NativeMethods.GetSystemMetrics(NativeMethods.VirtualScreenHeight));

    /// <summary>The monitor the mouse cursor is on, or the nearest one.</summary>
    public static PixelRect CursorMonitor()
    {
        NativeMethods.GetCursorPos(out NativeMethods.Point cursor);
        nint monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MonitorDefaultToNearest);
        NativeMethods.MonitorInfo info = new() { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (monitor == 0 || !NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return VirtualScreen();
        }

        return new PixelRect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top);
    }

    /// <summary>Copies a rectangle of the screen (virtual screen coordinates) with BitBlt from the screen's device context.</summary>
    public static ScreenFrame Capture(PixelRect area)
    {
        using Dib dib = Dib.Create(area.Width, area.Height);
        nint screen = NativeMethods.GetDC(0);
        try
        {
            if (!NativeMethods.BitBlt(dib.Context, 0, 0, area.Width, area.Height, screen, area.X, area.Y, NativeMethods.SrcCopyCaptureBlt))
            {
                throw new InvalidOperationException("Windows refused to copy the screen.");
            }
        }
        finally
        {
            NativeMethods.ReleaseDC(0, screen);
        }

        return new ScreenFrame(area.X, area.Y, area.Width, area.Height, dib.ReadOpaque());
    }
}

/// <summary>
/// Makes the current thread per-monitor DPI aware for the scope, so Win32 geometry is in physical pixels even when the
/// process manifest says otherwise (a test host, for example).
/// </summary>
internal readonly struct DpiScope : IDisposable
{
    private readonly nint _previous;

    private DpiScope(nint previous) => _previous = previous;

    public static DpiScope PerMonitor() => new(NativeMethods.SetThreadDpiAwarenessContext(NativeMethods.PerMonitorAwareV2));

    public void Dispose()
    {
        if (_previous != 0)
        {
            NativeMethods.SetThreadDpiAwarenessContext(_previous);
        }
    }
}
