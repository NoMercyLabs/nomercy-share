namespace NoMercy.Share.Core;

/// <summary>The actions a hotkey or a bar button can start.</summary>
public enum ShareAction
{
    Region,
    Window,
    FullScreen,
    Record,
    WebPage
}

public sealed class HotkeyPressedEventArgs(ShareAction action) : EventArgs
{
    public ShareAction Action { get; } = action;
}

/// <summary>Keyboard shortcuts that work while another app has the focus. Each platform has its own implementation.</summary>
public interface IGlobalHotkeys : IDisposable
{
    /// <summary>Binds a gesture such as "Ctrl+Shift+S" to an action; throws when the system refuses it.</summary>
    void Register(ShareAction action, string gesture);

    event EventHandler<HotkeyPressedEventArgs>? Pressed;
}
