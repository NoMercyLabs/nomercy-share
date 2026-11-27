using System.Buffers.Binary;
using System.Runtime.InteropServices;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

/// <summary>A fact that skips itself when the machine has no interactive Windows desktop (a service session, a container, another OS).</summary>
public sealed class DesktopFactAttribute : FactAttribute
{
    public DesktopFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive || Desktop.VirtualScreen().Width <= 0)
        {
            Skip = "No interactive Windows desktop on this machine.";
        }
    }
}

internal static partial class Desktop
{
    private const int PerMonitorAwareV2 = -4;

    private const int VirtualLeft = 76;

    [LibraryImport("user32.dll")]
    private static partial nint SetThreadDpiAwarenessContext(nint context);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    /// <summary>The virtual screen from its own Win32 calls, apart from the code under test.</summary>
    public static PixelRect VirtualScreen()
    {
        nint previous = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            return new PixelRect(GetSystemMetrics(VirtualLeft), GetSystemMetrics(VirtualLeft + 1), GetSystemMetrics(VirtualLeft + 2), GetSystemMetrics(VirtualLeft + 3));
        }
        finally
        {
            SetThreadDpiAwarenessContext(previous);
        }
    }
}

public sealed class WindowsScreenCaptureTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-windows-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private WindowsScreenCapture Capture(IRegionSelector selector, FullScreenArea area = FullScreenArea.VirtualScreen)
    {
        CaptureSettings settings = new() { Folder = _folder, FullScreenArea = area };
        return new WindowsScreenCapture(new CaptureFiles(settings, "unused", () => DateTimeOffset.UtcNow), selector, settings);
    }

    [DesktopFact]
    public async Task TheFullVirtualScreenIsSavedAsAPngOfTheScreenSize()
    {
        PixelRect screen = Desktop.VirtualScreen();

        CaptureResult result = await Capture(new PickRegion(null)).CaptureFullScreenAsync();

        Assert.Equal((screen.Width, screen.Height), (result.Width, result.Height));
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(_folder, Path.GetDirectoryName(result.FilePath));
        byte[] png = await File.ReadAllBytesAsync(result.FilePath);
        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], png[..8]);
        Assert.Equal((screen.Width, screen.Height), (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20))));
        Assert.True(png.Length > 100);
    }

    [DesktopFact]
    public async Task AMonitorCaptureIsNoLargerThanTheVirtualScreen()
    {
        PixelRect screen = Desktop.VirtualScreen();

        CaptureResult result = await Capture(new PickRegion(null), FullScreenArea.CursorMonitor).CaptureFullScreenAsync();

        Assert.InRange(result.Width, 1, screen.Width);
        Assert.InRange(result.Height, 1, screen.Height);
    }

    [DesktopFact]
    public async Task ARegionIsCutFromTheFrozenScreenAtTheChosenSize()
    {
        PixelRect screen = Desktop.VirtualScreen();
        PixelRect chosen = new(screen.X + 10, screen.Y + 20, 120, 60);

        CaptureResult result = await Capture(new PickRegion(chosen)).CaptureRegionAsync();

        Assert.Equal((120, 60), (result.Width, result.Height));
        byte[] png = await File.ReadAllBytesAsync(result.FilePath);
        Assert.Equal((120, 60), (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20))));
    }

    [DesktopFact]
    public async Task ACancelledRegionSavesNothing()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() => Capture(new PickRegion(null)).CaptureRegionAsync());

        Assert.False(Directory.Exists(_folder) && Directory.GetFiles(_folder).Length > 0);
    }

    private sealed class PickRegion(PixelRect? chosen) : IRegionSelector
    {
        public Task<PixelRect?> SelectAsync(ScreenFrame frozen, CancellationToken cancellationToken = default) => Task.FromResult(chosen);
    }
}
