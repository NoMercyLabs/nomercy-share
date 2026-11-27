namespace NoMercy.Share.Core.Settings;

/// <summary>How a screen recording is made and how small it is squeezed. Every limit is here, none is in the code.</summary>
public sealed record RecordingSettings
{
    /// <summary>The ffmpeg file to run; blank means look for ffmpeg on the PATH.</summary>
    public string FfmpegPath { get; init; } = ShareDefaults.RecordingFfmpegPath;

    /// <summary>The largest recording that gets shared, in megabytes. A longer clip is squeezed harder to fit.</summary>
    public double MaxSizeMegabytes { get; init; } = ShareDefaults.RecordingMaxSizeMegabytes;

    /// <summary>The part of <see cref="MaxSizeMegabytes"/> the video may use; the rest is room for the container and for rate control that overshoots.</summary>
    public double SizeHeadroom { get; init; } = ShareDefaults.RecordingSizeHeadroom;

    /// <summary>The most bits spent on one pixel of one frame. It keeps a short clip from getting a wasteful bitrate.</summary>
    public double TargetBitsPerPixel { get; init; } = ShareDefaults.RecordingTargetBitsPerPixel;

    public int FrameRate { get; init; } = ShareDefaults.RecordingFrameRate;

    public int MaxWidth { get; init; } = ShareDefaults.RecordingMaxWidth;

    public int MaxHeight { get; init; } = ShareDefaults.RecordingMaxHeight;

    /// <summary>The audio bitrate, taken off the size budget when a recording has sound (a later step).</summary>
    public int AudioBitrateKbps { get; init; } = ShareDefaults.RecordingAudioBitrateKbps;

    /// <summary>How long ffmpeg gets to close the recording file after the stop before it is killed.</summary>
    public int StopTimeoutSeconds { get; init; } = ShareDefaults.RecordingStopTimeoutSeconds;

    /// <summary>How long the squeeze may run before it is killed.</summary>
    public int CompressTimeoutSeconds { get; init; } = ShareDefaults.RecordingCompressTimeoutSeconds;
}
