namespace NoMercy.Share.Core.Capture;

/// <summary>The four sides of the border around a recorded rectangle, and the rectangle that holds them, in physical pixels.</summary>
public readonly record struct RecordingBorder(ScreenRect Bounds, ScreenRect Top, ScreenRect Bottom, ScreenRect Left, ScreenRect Right)
{
    /// <summary>
    /// Each side lies <paramref name="gap"/> pixels outside <paramref name="recorded"/>. A side with no room for that on
    /// <paramref name="monitor"/> lies just inside that edge of the recorded rectangle.
    /// </summary>
    public static RecordingBorder Around(ScreenRect recorded, ScreenRect monitor, int thickness, int gap)
    {
        int outside = thickness + gap;
        int left = recorded.X - outside >= monitor.X ? recorded.X - outside : recorded.X;
        int top = recorded.Y - outside >= monitor.Y ? recorded.Y - outside : recorded.Y;
        int right = recorded.Right + outside <= monitor.Right ? recorded.Right + outside : recorded.Right;
        int bottom = recorded.Bottom + outside <= monitor.Bottom ? recorded.Bottom + outside : recorded.Bottom;
        int width = right - left;
        int between = Math.Max(0, bottom - top - (2 * thickness));
        return new RecordingBorder(
            new ScreenRect(left, top, width, bottom - top),
            new ScreenRect(left, top, width, thickness),
            new ScreenRect(left, bottom - thickness, width, thickness),
            new ScreenRect(left, top + thickness, thickness, between),
            new ScreenRect(right - thickness, top + thickness, thickness, between));
    }
}
