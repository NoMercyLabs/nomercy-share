using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Bar;

/// <summary>A visible screen area in pixels (the part not covered by the taskbar).</summary>
public sealed record ScreenArea(double X, double Y, double Width, double Height);

/// <summary>Keeps the bar on a visible screen.</summary>
public static class BarPlacement
{
    /// <summary>Moves the wanted position so the whole bar lies inside the screen area it is nearest to.</summary>
    public static BarPosition Clamp(BarPosition wanted, double width, double height, IReadOnlyList<ScreenArea> areas)
    {
        if (areas.Count == 0)
        {
            return wanted;
        }

        double centerX = wanted.X + (width / 2);
        double centerY = wanted.Y + (height / 2);
        ScreenArea area = areas.MinBy(a => Distance(a, centerX, centerY))!;
        double x = Math.Clamp(wanted.X, area.X, Math.Max(area.X, area.X + area.Width - width));
        double y = Math.Clamp(wanted.Y, area.Y, Math.Max(area.Y, area.Y + area.Height - height));
        return new BarPosition(x, y);
    }

    /// <summary>The first-open position: centered at the top of the area, below the margin.</summary>
    public static BarPosition FirstOpen(ScreenArea area, double width, double topMargin) =>
        new(area.X + ((area.Width - width) / 2), area.Y + topMargin);

    /// <summary>Zero when the point lies inside the area, else the straight distance to its nearest edge.</summary>
    private static double Distance(ScreenArea area, double x, double y)
    {
        double dx = Math.Max(Math.Max(area.X - x, 0), x - (area.X + area.Width));
        double dy = Math.Max(Math.Max(area.Y - y, 0), y - (area.Y + area.Height));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
