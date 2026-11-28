namespace NoMercy.Share.Core;

/// <summary>Keeps one of the app's windows out of every screenshot and recording while the user still sees it.</summary>
public interface ICaptureExclusion
{
    /// <summary>Applies to the window with this native handle; false when the system did not take it.</summary>
    bool Exclude(nint window);

    /// <summary>Lets captures see the window again; false when the system did not take it.</summary>
    bool Include(nint window);
}

/// <summary>For a platform that cannot keep a window out of captures.</summary>
public sealed class NoCaptureExclusion : ICaptureExclusion
{
    public bool Exclude(nint window) => false;

    public bool Include(nint window) => false;
}
