using NoMercy.Share.Core;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Keeps a window out of screenshots and recordings with its display affinity (Windows 10 2004 or later).</summary>
public sealed class WindowsCaptureExclusion : ICaptureExclusion
{
    public bool Exclude(nint window) =>
        window != 0 && NativeMethods.SetWindowDisplayAffinity(window, NativeMethods.ExcludeFromCapture);

    public bool Include(nint window) =>
        window != 0 && NativeMethods.SetWindowDisplayAffinity(window, NativeMethods.NoDisplayAffinity);
}
