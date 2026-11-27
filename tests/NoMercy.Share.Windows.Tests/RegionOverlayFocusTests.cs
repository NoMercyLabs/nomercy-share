using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NoMercy.Share.Windows.Tests;

/// <summary>Starts the built app on its own data folder and sends the keys a user sends, so it runs only when NOMERCY_SHARE_INPUT_TEST=1 asks for it. No screenshot is taken: the overlay shows the screen.</summary>
public sealed partial class RegionOverlayFocusTests
{
    private const string OverlayTitle = "Select a region of the screen";

    private const ushort VkLeftWin = 0x5B;

    private const ushort VkShift = 0x10;

    private const ushort VkS = 0x53;

    private const ushort VkEscape = 0x1B;

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(2);

    [InputInjectionFact]
    public void WinShiftSTakesKeyboardFocusSoARealEscapeClosesTheOverlay()
    {
        string data = Path.Combine(Path.GetTempPath(), "nomercy-share-overlay-focus-" + Guid.NewGuid().ToString("N"));
        string captures = Path.Combine(data, "captures");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "settings.json"), "{\"NativeHost\":{\"Register\":false},\"Capture\":{\"Folder\":\"" + captures.Replace("\\", "/", StringComparison.Ordinal) + "\"}}");
        ProcessStartInfo start = new(AppExecutable()) { UseShellExecute = false };
        start.Environment["NOMERCY_SHARE_DATA"] = data;
        using Process app = Process.Start(start) ?? throw new InvalidOperationException("The app did not start.");
        try
        {
            Until(TimeSpan.FromSeconds(30), () => app.MainWindowHandle != 0 || Windows(app.Id).Count > 0);
            Thread.Sleep(2000); // the hook starts a moment after the bar shows

            SendKeys((VkLeftWin, false), (VkShift, false), (VkS, false), (VkS, true), (VkShift, true), (VkLeftWin, true));
            bool opened = Until(TimeSpan.FromSeconds(5), () => Windows(app.Id).Any(window => window.Title == OverlayTitle));
            bool focused = Until(Wait, () => ProcessOf(GetForegroundWindow()) == app.Id);

            bool closed = false;
            if (focused)
            {
                SendKeys((VkEscape, false), (VkEscape, true));
                closed = Until(Wait, () => Windows(app.Id).All(window => window.Title != OverlayTitle));
            }

            Assert.True(opened, "The overlay did not open.");
            Assert.True(focused, $"The foreground window belongs to process {ProcessOf(GetForegroundWindow())}, not the app ({app.Id}).");
            Assert.True(closed, "A real Escape did not close the overlay.");
            Assert.Empty(Directory.Exists(captures) ? Directory.GetFiles(captures, "*", SearchOption.AllDirectories) : []);
            Assert.Empty(new[] { "ScreenClippingHost", "SnippingTool" }.SelectMany(Process.GetProcessesByName));
        }
        finally
        {
            if (!app.HasExited)
            {
                app.Kill(entireProcessTree: true);
                app.WaitForExit(5000);
            }

            Directory.Delete(data, recursive: true);
        }
    }

    private static string AppExecutable()
    {
        string? root = AppContext.BaseDirectory;
        while (root is not null && !Directory.Exists(Path.Combine(root, "src", "NoMercy.Share.App")))
        {
            root = Path.GetDirectoryName(root);
        }

        string output = Path.Combine(root ?? throw new InvalidOperationException("The repository root was not found."), "src", "NoMercy.Share.App", "bin");
        return Directory.GetFiles(output, "nomercy-share.exe", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault() ?? throw new FileNotFoundException("Build NoMercy.Share.App first.");
    }

    private static bool Until(TimeSpan limit, Func<bool> condition)
    {
        DateTime end = DateTime.UtcNow + limit;
        while (DateTime.UtcNow < end)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return condition();
    }

    private static int ProcessOf(nint window)
    {
        GetWindowThreadProcessId(window, out uint process);
        return (int)process;
    }

    private static List<(nint Handle, string Title)> Windows(int process)
    {
        List<(nint, string)> found = [];
        EnumWindows((window, _) =>
        {
            if (ProcessOf(window) == process && IsWindowVisible(window))
            {
                StringBuilder title = new(256);
                GetWindowText(window, title, 256);
                found.Add((window, title.ToString()));
            }

            return true;
        }, 0);
        return found;
    }

    private static void SendKeys(params (ushort Key, bool Up)[] keys)
    {
        Input[] inputs = [.. keys.Select(key => new Input { Type = 1, Vk = key.Key, Flags = key.Up ? 2u : 0u })];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public ushort Vk;

        [FieldOffset(10)]
        public ushort Scan;

        [FieldOffset(12)]
        public uint Flags;
    }

    private delegate bool EnumWindowsProc(nint window, nint parameter);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint process);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int max);
}
