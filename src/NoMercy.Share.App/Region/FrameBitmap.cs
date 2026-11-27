using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>Turns a captured frame into a bitmap Avalonia can draw.</summary>
internal static class FrameBitmap
{
    private const double ScreenDpi = 96;

    public static WriteableBitmap From(ScreenFrame frame)
    {
        WriteableBitmap bitmap = new(new PixelSize(frame.Width, frame.Height), new Vector(ScreenDpi, ScreenDpi), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using ILockedFramebuffer buffer = bitmap.Lock();
        int rowBytes = frame.Width * ScreenFrame.BytesPerPixel;
        for (int row = 0; row < frame.Height; row++)
        {
            Marshal.Copy(frame.Bgra, row * rowBytes, buffer.Address + (row * buffer.RowBytes), rowBytes);
        }

        return bitmap;
    }
}
