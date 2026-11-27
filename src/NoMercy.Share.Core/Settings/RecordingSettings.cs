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

    /// <summary>The widest picture a recording is shared at; 0 (with <see cref="MaxHeight"/> 0) keeps the screen's own size.</summary>
    public int MaxWidth { get; init; } = ShareDefaults.RecordingMaxWidth;

    public int MaxHeight { get; init; } = ShareDefaults.RecordingMaxHeight;

    /// <summary>The video codec of the shared file: "h264", "hevc" or "av1".</summary>
    public string Codec { get; init; } = ShareDefaults.RecordingCodec;

    /// <summary>What encodes it: "cpu", "nvidia", "intel" or "amd". A GPU encode that fails is done again on the CPU.</summary>
    public string Encoder { get; init; } = ShareDefaults.RecordingEncoder;

    /// <summary>"fast", "balanced" or "quality": a slower encode gives a sharper picture at the same size.</summary>
    public string Speed { get; init; } = ShareDefaults.RecordingSpeed;

    /// <summary>The audio bitrate, taken off the size budget when a recording has sound.</summary>
    public int AudioBitrateKbps { get; init; } = ShareDefaults.RecordingAudioBitrateKbps;

    /// <summary>Which sound goes into a recording.</summary>
    public AudioSettings Audio { get; init; } = new();

    /// <summary>How long a recording with sound waits for the first video frame before the sound starts anyway.</summary>
    public int FirstFrameWaitMilliseconds { get; init; } = ShareDefaults.RecordingFirstFrameWaitMilliseconds;

    /// <summary>How long ffmpeg gets to close the recording file after the stop before it is killed.</summary>
    public int StopTimeoutSeconds { get; init; } = ShareDefaults.RecordingStopTimeoutSeconds;

    /// <summary>How long the squeeze may run before it is killed.</summary>
    public int CompressTimeoutSeconds { get; init; } = ShareDefaults.RecordingCompressTimeoutSeconds;

    /// <summary>How many more times a video that is still over <see cref="MaxSizeMegabytes"/> is encoded; 0 means never.</summary>
    public int MaxCompressAttempts { get; init; } = ShareDefaults.RecordingMaxCompressAttempts;

    /// <summary>How long after the start ffmpeg is watched; quitting inside it fails the start.</summary>
    public int StartupCheckMilliseconds { get; init; } = ShareDefaults.RecordingStartupCheckMilliseconds;
}
