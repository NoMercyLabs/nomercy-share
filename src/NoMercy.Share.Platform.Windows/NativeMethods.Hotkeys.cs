using System.Runtime.InteropServices;

namespace NoMercy.Share.Platform.Windows;

/// <summary>The user32 calls of the global hotkeys: a hidden message-only window that receives WM_HOTKEY.</summary>
internal static partial class NativeMethods
{
    internal const uint ModAlt = 1;

    internal const uint ModControl = 2;

    internal const uint ModShift = 4;

    internal const uint ModWin = 8;

    /// <summary>Holding the keys down raises one press, not a stream of them.</summary>
    internal const uint ModNoRepeat = 0x4000;

    internal const uint WmHotkey = 0x0312;

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
}
