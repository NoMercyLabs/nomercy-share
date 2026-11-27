using NoMercy.Share.Core;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Global hotkeys on Windows. Not built yet: the hotkey step fills this in.</summary>
public sealed class WindowsGlobalHotkeys : IGlobalHotkeys
{
    public event EventHandler<HotkeyPressedEventArgs>? Pressed;

    public void Register(ShareAction action, string gesture) =>
        throw new NotImplementedException("Windows global hotkeys arrive in the hotkey step.");

    public void Dispose()
    {
        Pressed = null;
    }
}
