using Avalonia.Threading;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>Shows the recording border while the recorder reports a recorded rectangle, and closes it when it reports none.</summary>
public sealed class RecordingBorderPresenter
{
    private readonly Func<IReadOnlyList<(ScreenRect Bounds, double Scale)>> _monitors;
    private readonly ICaptureExclusion _exclusion;
    private readonly IInputPassThrough _passThrough;

    public RecordingBorderPresenter(IScreenRecorder recorder, Func<IReadOnlyList<(ScreenRect Bounds, double Scale)>> monitors, ICaptureExclusion exclusion, IInputPassThrough passThrough)
    {
        _monitors = monitors;
        _exclusion = exclusion;
        _passThrough = passThrough;
        recorder.RecordedAreaChanged += (_, area) => Dispatcher.UIThread.Post(() => Present(area));
    }

    /// <summary>The border on screen; null when nothing is recorded.</summary>
    public RecordingBorderWindow? Window { get; private set; }

    private void Present(ScreenRect? area)
    {
        Window?.Close();
        Window = null;
        if (area is not { } recorded)
        {
            return;
        }

        foreach ((ScreenRect bounds, double scale) in _monitors())
        {
            if (!bounds.Intersect(recorded).IsEmpty)
            {
                Window = new RecordingBorderWindow(recorded, bounds, scale, _exclusion, _passThrough);
                Window.Show();
                return;
            }
        }
    }
}
