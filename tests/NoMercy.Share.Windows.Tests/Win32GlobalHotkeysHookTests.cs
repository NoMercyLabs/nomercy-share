using System.Diagnostics;
using System.Runtime.InteropServices;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

/// <summary>A fact for the one test that sends real keys to the desktop, so it runs only when NOMERCY_SHARE_INPUT_TEST=1 asks for it.</summary>
public sealed class InputInjectionFactAttribute : FactAttribute
{
    public const string OptInVariable = "NOMERCY_SHARE_INPUT_TEST";

    public InputInjectionFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
        {
            Skip = $"Sends keys to the desktop; set {OptInVariable}=1 to run it.";
        }
        else if (!OperatingSystem.IsWindows() || !Environment.UserInteractive || Desktop.VirtualScreen().Width <= 0)
        {
            Skip = "No interactive Windows desktop on this machine.";
        }
    }
}

/// <summary>Win+Shift+S belongs to the shell, so Register must serve it from the keyboard hook; the hook itself is checked without keys.</summary>
public sealed partial class Win32GlobalHotkeysHookTests
{
    private const uint WinShiftModifiers = 0x4008;

    private const uint KeyS = 0x53;

    private const ushort VkLeftWin = 0x5B;

    private const ushort VkShift = 0x10;

    private const ushort VkEscape = 0x1B;

    private static readonly HotkeyChord WinShiftS = HotkeyChord.Parse("Win+Shift+S");

    private static readonly HotkeyChord Free = HotkeyChord.Parse("Ctrl+Alt+Shift+F10"); // the other hotkey tests use F11 and F12 at the same time

    private static Dictionary<BarAction, HotkeyChord> Chords(HotkeyChord chord) => new() { [BarAction.Region] = chord };

    [DesktopFact]
    public void TheShellRefusesWinShiftSToRegisterHotKey()
    {
        bool registered = RegisterHotKey(0, 1, WinShiftModifiers, KeyS);
        if (registered)
        {
            UnregisterHotKey(0, 1);
        }

        Assert.False(registered, "RegisterHotKey accepted Win+Shift+S on this machine, so the hook path is not needed here.");
    }

    [DesktopFact]
    public void AChordTheShellOwnsIsServedByTheHookAndReportedRegistered()
    {
        using Win32GlobalHotkeys hotkeys = new();

        HotkeyRegistration result = Assert.Single(hotkeys.Register(Chords(WinShiftS)));

        Assert.True(result.Registered);
        Assert.True(hotkeys.HookInstalled);
    }

    [DesktopFact]
    public void AChordRegisterHotKeyAcceptsNeverInstallsTheHook()
    {
        using Win32GlobalHotkeys hotkeys = new();

        hotkeys.Register(Chords(Free));

        Assert.False(hotkeys.HookInstalled);
    }

    [DesktopFact]
    public void RegisteringAgainWithoutTheChordRemovesTheHook()
    {
        using Win32GlobalHotkeys hotkeys = new();
        hotkeys.Register(Chords(WinShiftS));
        Assert.True(hotkeys.HookInstalled);

        hotkeys.Register(Chords(Free));

        Assert.False(hotkeys.HookInstalled);
    }

    [DesktopFact]
    public void DisposeRemovesTheHook()
    {
        Win32GlobalHotkeys hotkeys = new();
        hotkeys.Register(Chords(WinShiftS));

        hotkeys.Dispose();

        Assert.False(hotkeys.HookInstalled);
    }

    [InputInjectionFact]
    public void WinShiftSRaisesRegionOnceAndTheSnippingToolStaysClosed()
    {
        using Win32GlobalHotkeys hotkeys = new();
        List<BarAction> raised = [];
        hotkeys.Pressed += (_, action) => raised.Add(action);
        hotkeys.Register(Chords(WinShiftS));
        HashSet<int> before = SnippingProcessIds();

        SendKeys((VkLeftWin, false), (VkShift, false), ((ushort)KeyS, false), ((ushort)KeyS, true), (VkShift, true), (VkLeftWin, true));
        Pump(TimeSpan.FromSeconds(1));

        int[] opened = [.. SnippingProcessIds().Except(before)];
        string front = ForegroundProcessName();
        bool startOpened = front is "StartMenuExperienceHost" or "SearchHost" or "SearchApp";
        if (opened.Length > 0 || startOpened)
        {
            SendKeys((VkEscape, false), (VkEscape, true));
        }

        Assert.Equal([BarAction.Region], raised);
        Assert.Empty(opened);
        Assert.False(startOpened, $"The Start menu opened after the chord (foreground: {front}).");
    }

    private static string ForegroundProcessName()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint processId);
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    private static HashSet<int> SnippingProcessIds() =>
        [.. new[] { "ScreenClippingHost", "SnippingTool" }.SelectMany(Process.GetProcessesByName).Select(process => process.Id)];

    private static void SendKeys(params (ushort Key, bool Up)[] keys)
    {
        Input[] inputs = [.. keys.Select(key => new Input { Type = 1, Key = new KeyInput { VirtualKey = key.Key, Flags = key.Up ? 2u : 0u } })];
        Assert.Equal((uint)inputs.Length, SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()));
    }

    /// <summary>Runs this thread's message loop, which the keyboard hook and the posted press both need.</summary>
    private static void Pump(TimeSpan time)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (clock.Elapsed < time)
        {
            while (PeekMessage(out Message message, 0, 0, 0, 1))
            {
                TranslateMessage(in message);
                DispatchMessage(in message);
            }

            Thread.Sleep(10);
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint key);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint window, int id);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint count, [In] Input[] inputs, int size);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TranslateMessage(in Message message);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static partial nint DispatchMessage(in Message message);

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Window;
        public uint Id;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public KeyInput Key;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct KeyInput // the keyboard member of the INPUT union, which is as large as its mouse member
    {
        [FieldOffset(0)]
        public ushort VirtualKey;

        [FieldOffset(2)]
        public ushort ScanCode;

        [FieldOffset(4)]
        public uint Flags;
    }
}
