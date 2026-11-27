namespace NoMercy.Share.Core.Settings;

/// <summary>The default of every policy value. A user changes each one in settings.json; none of them is a literal in logic.</summary>
public static class ShareDefaults
{
    /// <summary>The storage provider used when settings name none. R2 is the first provider.</summary>
    public const string ProviderName = "r2";

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
