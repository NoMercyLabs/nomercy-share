namespace NoMercy.Share.Core.Settings;

/// <summary>Which sound goes into a screen recording. Both are off until the user turns them on.</summary>
public sealed record AudioSettings
{
    /// <summary>Record what the default sound output plays.</summary>
    public bool SystemSound { get; init; } = ShareDefaults.RecordingSystemSound;

    /// <summary>Record the default microphone.</summary>
    public bool Microphone { get; init; } = ShareDefaults.RecordingMicrophone;

    /// <summary>True when any sound is asked for.</summary>
    public bool Any => SystemSound || Microphone;
}
