namespace NoMercy.Share.Core;

/// <summary>Gives keyboard focus to one of the app's own windows when the app opened it without receiving the user's last input (a keyboard hook, not a window message).</summary>
public interface IForegroundGrant
{
    /// <summary>Brings the window with this native handle to the foreground. Does nothing for a handle of 0.</summary>
    void BringToForeground(nint window);
}

/// <summary>For a platform where opening a window already gives it focus.</summary>
public sealed class NoForegroundGrant : IForegroundGrant
{
    public void BringToForeground(nint window)
    {
    }
}
