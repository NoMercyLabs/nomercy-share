using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core;

/// <summary>Says which monitor the mouse is on, so the app's windows open where the user is looking.</summary>
public interface IPointerMonitor
{
    /// <summary>The monitor's bounds in physical pixels.</summary>
    ScreenRect Bounds();
}
