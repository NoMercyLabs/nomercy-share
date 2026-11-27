using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Screen pictures on Windows. Not built yet: the capture step fills this in.</summary>
public sealed class WindowsScreenCapture : IScreenCapture
{
    public Task<CaptureResult> CaptureRegionAsync(CancellationToken cancellationToken = default) => throw Later("region capture");

    public Task<CaptureResult> CaptureWindowAsync(CancellationToken cancellationToken = default) => throw Later("window capture");

    public Task<CaptureResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default) => throw Later("full screen capture");

    private static NotImplementedException Later(string what) => new($"Windows {what} arrives in the screen capture step.");
}
