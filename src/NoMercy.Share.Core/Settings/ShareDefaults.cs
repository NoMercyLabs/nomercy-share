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

    /// <summary>The gap between the screen's top edge and the bar the first time it opens.</summary>
    public const double FirstOpenTopMargin = 24;
}
