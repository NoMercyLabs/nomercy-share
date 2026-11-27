namespace NoMercy.Share.Core.Capture;

/// <summary>A rectangle in physical pixels. On the virtual screen X and Y can be negative (a monitor left of or above the main one).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public int Right => X + Width;

    public int Bottom => Y + Height;
}

/// <summary>The arithmetic of the region overlay, apart from any window so it can be tested.</summary>
public static class RegionMath
{
    /// <summary>
    /// The selection from two drag points in any direction. The points are device-independent units inside the overlay,
    /// <paramref name="scale"/> turns them into physical pixels, <paramref name="origin"/> (its X and Y) is where the overlay starts on the
    /// virtual screen, and the result is clamped to <paramref name="bounds"/>.
    /// </summary>
    public static PixelRect FromDrag(double ax, double ay, double bx, double by, double scale, PixelRect origin, PixelRect bounds)
    {
        int left = Math.Clamp(origin.X + (int)Math.Round(Math.Min(ax, bx) * scale), bounds.X, bounds.Right);
        int top = Math.Clamp(origin.Y + (int)Math.Round(Math.Min(ay, by) * scale), bounds.Y, bounds.Bottom);
        int right = Math.Clamp(origin.X + (int)Math.Round(Math.Max(ax, bx) * scale), bounds.X, bounds.Right);
        int bottom = Math.Clamp(origin.Y + (int)Math.Round(Math.Max(ay, by) * scale), bounds.Y, bounds.Bottom);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Where the size label goes: below the selection, else above it, else inside its top-left corner. It never leaves the area.
    /// All numbers are in the same unit (the overlay's).
    /// </summary>
    public static (double X, double Y) LabelPosition(PixelRect selection, double labelWidth, double labelHeight, double gap, double areaWidth, double areaHeight)
    {
        double x = Math.Clamp(selection.X, 0, Math.Max(0, areaWidth - labelWidth));
        double below = selection.Bottom + gap;
        if (below + labelHeight <= areaHeight)
        {
            return (x, below);
        }

        double above = selection.Y - gap - labelHeight;
        return above >= 0 ? (x, above) : (selection.X + gap, selection.Y + gap);
    }
}
