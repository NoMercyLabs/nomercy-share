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

    /// <summary>The gap between the screen's top edge and the bar the first time it opens.</summary>
    public const double FirstOpenTopMargin = 24;
}
