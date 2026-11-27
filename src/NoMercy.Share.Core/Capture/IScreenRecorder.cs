namespace NoMercy.Share.Core.Capture;

/// <summary>How a recording starts.</summary>
/// <param name="IncludeAudio">Record the microphone or system audio with the picture.</param>
public sealed record RecordingOptions(bool IncludeAudio = false);

/// <summary>Records the screen to a video file. Each platform has its own implementation.</summary>
public interface IScreenRecorder
{
    /// <summary>True between a successful start and the stop.</summary>
    bool IsRecording { get; }

    /// <summary>What the last start did without, such as a missing microphone, in words for the user; null when it started as asked.</summary>
    string? StartNotice { get; }

    /// <summary>Raised, on any thread, when the recording ends by itself instead of by a stop. The text says why. <see cref="IsRecording"/> is false by then.</summary>
    event EventHandler<string>? Failed;

    Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default);

    /// <summary>Ends the recording and returns the finished video file.</summary>
    Task<CaptureResult> StopAsync(CancellationToken cancellationToken = default);
}
