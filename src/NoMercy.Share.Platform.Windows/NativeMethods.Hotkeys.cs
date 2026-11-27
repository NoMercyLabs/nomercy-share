using System.Runtime.InteropServices;

namespace NoMercy.Share.Platform.Windows;

/// <summary>The user32 calls of the global hotkeys: a hidden message-only window that receives WM_HOTKEY, and the keyboard hook for the Win chords the shell owns.</summary>
internal static partial class NativeMethods
{
    internal const uint ModAlt = 1;

    internal const uint ModControl = 2;

    internal const uint ModShift = 4;

    internal const uint ModWin = 8;

    /// <summary>Holding the keys down raises one press, not a stream of them.</summary>
    internal const uint ModNoRepeat = 0x4000;

    internal const uint WmHotkey = 0x0312;

    /// <summary>WM_APP + 1: posted to the hotkey window by the keyboard hook when a chord it serves is pressed. wParam is the BarAction.</summary>
    internal const uint WmHookedChord = 0x8001;

    internal const int WhKeyboardLowLevel = 13;

    internal const int HcAction = 0;

    internal const uint WmKeyUp = 0x0101;

    internal const uint WmSysKeyUp = 0x0105;

    internal const int VkShift = 0x10;

    internal const int VkControl = 0x11;

    internal const int VkMenu = 0x12;

    internal const int VkLeftWin = 0x5B;

    internal const int VkRightWin = 0x5C;

    internal delegate nint HookProc(int code, nint wParam, nint lParam);

    /// <summary>HWND_MESSAGE: the parent of a window that only receives messages and is never drawn.</summary>
    internal static readonly nint MessageOnlyParent = -3;

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowClass
    {
        public uint Size;
        public uint Style;
        public nint WindowProc;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassName;
        public nint SmallIcon;
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static partial ushort RegisterClass(in WindowClass windowClass);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true)]
    internal static partial nint CreateWindow(uint extendedStyle, nint className, nint windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW")]
    internal static partial nint GetModuleHandle(nint name);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint key);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint window, int id);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static partial nint SetWindowsHookEx(int hook, nint callback, nint module, uint thread);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    internal static partial nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int virtualKey);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessage(nint window, uint message, nint wParam, nint lParam);
}
