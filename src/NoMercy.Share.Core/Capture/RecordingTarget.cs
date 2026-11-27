namespace NoMercy.Share.Core.Capture;

/// <summary>What to record: one monitor (by its index) and a rectangle on it, with offsets from the monitor's own top-left corner, in physical pixels.</summary>
public readonly record struct RecordingTarget(int MonitorIndex, int OffsetX, int OffsetY, int Width, int Height)
{
    /// <summary>The monitor the region covers most, with the region cut at that monitor's edge. Desktop Duplication reads one monitor at a time.</summary>
    public static RecordingTarget For(ScreenRect region, IReadOnlyList<ScreenRect> monitors)
    {
        int best = -1;
        long bestArea = 0;
        for (int index = 0; index < monitors.Count; index++)
        {
            ScreenRect shared = region.Intersect(monitors[index]);
            long area = shared.IsEmpty ? 0 : (long)shared.Width * shared.Height;
            if (area > bestArea)
            {
                best = index;
                bestArea = area;
            }
        }

        if (best < 0)
        {
            throw new RecordingException(Strings.Get("error.recording.region"));
        }

        ScreenRect monitor = monitors[best];
        ScreenRect cut = region.Intersect(monitor);
        return new RecordingTarget(best, cut.X - monitor.X, cut.Y - monitor.Y, cut.Width, cut.Height);
    }
}
