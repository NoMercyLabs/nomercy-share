namespace NoMercy.Share.Core.Capture;

/// <summary>A rectangle in physical pixels. On the virtual screen X and Y can be negative (a monitor left of or above the main one).</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public int Right => X + Width;

    public int Bottom => Y + Height;

    /// <summary>The part both rectangles cover. It is empty when they do not touch.</summary>
    public ScreenRect Intersect(ScreenRect other)
    {
        int left = Math.Max(X, other.X);
        int top = Math.Max(Y, other.Y);
        return new ScreenRect(left, top, Math.Min(Right, other.Right) - left, Math.Min(Bottom, other.Bottom) - top);
    }
}
