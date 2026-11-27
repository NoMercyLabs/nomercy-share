using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Screen recording on Windows. Not built yet: the recording step fills this in.</summary>
public sealed class WindowsScreenRecorder : IScreenRecorder
{
    public bool IsRecording => false;

    public Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Windows screen recording arrives in the screen recording step.");

    public Task<CaptureResult> StopAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Windows screen recording arrives in the screen recording step.");
}
