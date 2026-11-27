using System.Runtime.InteropServices;
using NoMercy.Share.Core.Hotkeys;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// A low-level keyboard hook that serves only the chords of a <see cref="HookChordMatcher"/>. Install and dispose it on the
/// UI thread: that thread's message loop runs the callback. The callback stores and logs nothing; an event that is not
/// one of the chords goes straight on. A match is posted to the window, never handled inside the callback.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private readonly HookChordMatcher _matcher;
    private readonly nint _window;
    private readonly NativeMethods.HookProc _callback; // a field, so the garbage collector keeps the native thunk alive
    private nint _handle;

    private KeyboardHook(HookChordMatcher matcher, nint window)
    {
        _matcher = matcher;
        _window = window;
        _callback = OnKey;
    }

    /// <summary>Installs the hook, or returns null when Windows refuses it.</summary>
    public static KeyboardHook? TryInstall(HookChordMatcher matcher, nint window)
    {
        KeyboardHook hook = new(matcher, window);
        hook._handle = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLowLevel, Marshal.GetFunctionPointerForDelegate(hook._callback), NativeMethods.GetModuleHandle(0), 0);
        return hook._handle == 0 ? null : hook;
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_handle);
            _handle = 0;
        }
    }

    private nint OnKey(int code, nint wParam, nint lParam)
    {
        if (code == NativeMethods.HcAction)
        {
            try
            {
                uint key = (uint)Marshal.ReadInt32(lParam);
                bool keyUp = (uint)wParam is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
                HookDecision decision = _matcher.Decide(key, keyUp, HeldModifiers());
                if (decision.Action is { } action)
                {
                    NativeMethods.PostMessage(_window, NativeMethods.WmHookedChord, (nint)action, 0);
                }

                if (decision.Swallow)
                {
                    return 1;
                }
            }
            catch (Exception)
            {
                // a failure here must never block the keyboard: the event goes on
            }
        }

        return NativeMethods.CallNextHookEx(_handle, code, wParam, lParam);
    }

    private static HotkeyModifiers HeldModifiers() =>
        (Down(NativeMethods.VkControl) ? HotkeyModifiers.Ctrl : HotkeyModifiers.None)
        | (Down(NativeMethods.VkMenu) ? HotkeyModifiers.Alt : HotkeyModifiers.None)
        | (Down(NativeMethods.VkShift) ? HotkeyModifiers.Shift : HotkeyModifiers.None)
        | (Down(NativeMethods.VkLeftWin) || Down(NativeMethods.VkRightWin) ? HotkeyModifiers.Win : HotkeyModifiers.None);

    private static bool Down(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
}
