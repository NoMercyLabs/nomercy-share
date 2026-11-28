using NoMercy.Share.Core;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// Passes input through a window with its extended styles. Apply <see cref="WindowsCaptureExclusion"/> first: Windows refuses
/// that on a window that already has the layered style.
/// </summary>
public sealed class WindowsInputPassThrough : IInputPassThrough
{
    public bool Apply(nint window)
    {
        if (window == 0)
        {
            return false;
        }

        nint styles = NativeMethods.GetWindowLongPtr(window, NativeMethods.ExtendedStyle);
        nint wanted = styles | NativeMethods.PassThroughStyles;
        NativeMethods.SetWindowLongPtr(window, NativeMethods.ExtendedStyle, wanted);
        return (NativeMethods.GetWindowLongPtr(window, NativeMethods.ExtendedStyle) & NativeMethods.PassThroughStyles) == NativeMethods.PassThroughStyles;
    }
}
