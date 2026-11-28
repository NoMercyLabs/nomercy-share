namespace NoMercy.Share.Core;

/// <summary>Makes one of the app's windows one the user only looks at: the mouse goes through it, it never takes focus, and it has no taskbar entry.</summary>
public interface IInputPassThrough
{
    /// <summary>Applies to the window with this native handle; false when the system did not take it.</summary>
    bool Apply(nint window);
}
