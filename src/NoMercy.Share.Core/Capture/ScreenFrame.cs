namespace NoMercy.Share.Core.Capture;

/// <summary>
/// A picture of part of the virtual screen: blue, green, red, alpha bytes per pixel, row after row with no padding.
/// <see cref="X"/> and <see cref="Y"/> are where it sits on the virtual screen in physical pixels.
/// </summary>
public sealed record ScreenFrame(int X, int Y, int Width, int Height, byte[] Bgra)
{
    public const int BytesPerPixel = 4;

    public PixelRect Bounds => new(X, Y, Width, Height);

    /// <summary>The part inside <paramref name="area"/>, given in virtual screen coordinates.</summary>
    public ScreenFrame Crop(PixelRect area)
    {
        if (area.IsEmpty || area.X < X || area.Y < Y || area.Right > X + Width || area.Bottom > Y + Height)
        {
            throw new ArgumentOutOfRangeException(nameof(area), area, "The area must be non-empty and inside the frame.");
        }

        byte[] pixels = new byte[area.Width * area.Height * BytesPerPixel];
        int rowBytes = area.Width * BytesPerPixel;
        for (int row = 0; row < area.Height; row++)
        {
            int from = (((area.Y - Y + row) * Width) + (area.X - X)) * BytesPerPixel;
            Buffer.BlockCopy(Bgra, from, pixels, row * rowBytes, rowBytes);
        }

        return new ScreenFrame(area.X, area.Y, area.Width, area.Height, pixels);
    }
}
