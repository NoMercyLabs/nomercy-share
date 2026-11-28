using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>
/// The border the user sees around the recorded rectangle. Clicks and keys pass through it, it never takes focus, and it is kept
/// out of captures. A side that lies inside the rectangle shows only when that worked.
/// </summary>
public partial class RecordingBorderWindow : Window
{
    /// <summary>In device-independent units, so it looks the same at every display scale.</summary>
    private const double SideThickness = 3;

    private const double DistanceFromRecording = 2;

    public RecordingBorderWindow()
    {
        InitializeComponent();
    }

    /// <param name="recorded">The recorded rectangle on the virtual screen, in physical pixels.</param>
    /// <param name="monitor">The bounds of the monitor it is on.</param>
    /// <param name="scale">That monitor's display scale: physical pixels per device-independent unit.</param>
    public RecordingBorderWindow(ScreenRect recorded, ScreenRect monitor, double scale, ICaptureExclusion exclusion, IInputPassThrough passThrough) : this()
    {
        RecordingBorder border = RecordingBorder.Around(recorded, monitor, Pixels(SideThickness, scale), Pixels(DistanceFromRecording, scale));
        Position = new PixelPoint(border.Bounds.X, border.Bounds.Y);
        Width = border.Bounds.Width / scale;
        Height = border.Bounds.Height / scale;
        (Border Control, ScreenRect Area)[] sides = [(TopSide, border.Top), (BottomSide, border.Bottom), (LeftSide, border.Left), (RightSide, border.Right)];
        foreach ((Border control, ScreenRect area) in sides)
        {
            Canvas.SetLeft(control, (area.X - border.Bounds.X) / scale);
            Canvas.SetTop(control, (area.Y - border.Bounds.Y) / scale);
            control.Width = area.Width / scale;
            control.Height = area.Height / scale;
        }

        // Every side starts hidden, so none can reach a capture before the window is kept out of it.
        Opened += (_, _) =>
        {
            nint handle = TryGetPlatformHandle()?.Handle ?? 0;
            bool excluded = exclusion.Exclude(handle);
            // Only after the exclusion, which Windows refuses once the window lets input through.
            if (!passThrough.Apply(handle))
            {
                Trace.TraceWarning("The recording border does not let the mouse through.");
            }

            if (!excluded)
            {
                Trace.TraceWarning("The recording border could not be left out of screen captures, so no side of it lies inside the recording.");
            }

            foreach ((Border control, ScreenRect area) in sides)
            {
                control.IsVisible = excluded || area.Intersect(recorded).IsEmpty;
            }
        };
    }

    private static int Pixels(double units, double scale) => (int)Math.Round(units * scale, MidpointRounding.AwayFromZero);
}
