namespace NoMercy.Share.Core.Settings;

/// <summary>The default of every setting a user can change in settings.json. Rules that are not settings live next to the code that uses them.</summary>
public static class ShareDefaults
{
    /// <summary>The storage provider used when settings name none. R2 is the first provider.</summary>
    public const string ProviderName = "r2";

    /// <summary>What the clipboard holds after a capture is shared.</summary>
    public const ClipboardContent ClipboardContent = Settings.ClipboardContent.Link;

    /// <summary>The signing region of the storage service; R2 uses "auto".</summary>
    public const string Region = "auto";

    public const bool ForcePathStyle = true;

    /// <summary>The length of the random object id. 62^10 is about 8e17 ids, so a link cannot be guessed.</summary>
    public const int KeyLength = 10;

    /// <summary>The lifetimes that have an expiry prefix, in days. The host needs one lifecycle rule per prefix.</summary>
    public static readonly int[] ExpiryBucketDays = [1, 7, 30, 90];

    public const string ExpiryPrefixPattern = "exp-{0}d";

    public const string KeepPrefix = "keep";

    public const string SidecarSuffix = ".json";

    /// <summary>How many days a link lives unless the upload says otherwise. Null in the settings means never.</summary>
    public const int ExpiryDays = 30;

    /// <summary>How long a short message on the bar stays visible.</summary>
    public const int NoticeSeconds = 3;

    /// <summary>The folder name under the user's Pictures folder that holds captures when settings name none.</summary>
    public const string CaptureFolderName = "NoMercy Share";

    /// <summary>The capture file name: UTC and sortable, with milliseconds so two captures in one second differ.</summary>
    public const string CaptureFileNamePattern = "nomercy-share-{0:yyyyMMdd'T'HHmmssfff}Z";

    /// <summary>The wait between hiding the bar and taking the picture.</summary>
    public const int CaptureHideDelayMilliseconds = 150;

    /// <summary>What a full-screen capture holds unless settings say otherwise.</summary>
    public const FullScreenArea FullScreenArea = Settings.FullScreenArea.CursorMonitor;

    public const string RecordingFfmpegPath = "";

    public const double RecordingMaxSizeMegabytes = 50;

    /// <summary>Bamo's headroom idea: aim under the limit (he used 9.5 for a 10 MB limit, 95 percent).</summary>
    public const double RecordingSizeHeadroom = 0.95;

    public const double RecordingTargetBitsPerPixel = 0.05;

    public const int RecordingFrameRate = 30;

    public const int RecordingMaxWidth = 1920;

    public const int RecordingMaxHeight = 1080;

    public const int RecordingAudioBitrateKbps = 128;

    public const int RecordingStopTimeoutSeconds = 15;

    public const int RecordingCompressTimeoutSeconds = 600;

    /// <summary>How often a video over the size cap is encoded again at a lower bitrate.</summary>
    public const int RecordingMaxCompressAttempts = 2;

    /// <summary>How long a new recording is watched for an ffmpeg that quits at once.</summary>
    public const int RecordingStartupCheckMilliseconds = 1000;

    /// <summary>The global hotkeys, chosen to be familiar to people who know ShareX. An empty value turns a hotkey off.</summary>
    public const string HotkeyRegion = "Ctrl+PrintScreen";

    public const string HotkeyFullScreen = "PrintScreen";

    public const string HotkeyWindow = "Alt+PrintScreen";

    public const string HotkeyRecord = "Shift+PrintScreen";

    /// <summary>The gap between the screen's top edge and the bar the first time it opens.</summary>
    public const double FirstOpenTopMargin = 24;
}
