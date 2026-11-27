namespace NoMercy.Share.Core.Settings;

/// <summary>How much the recent links list remembers.</summary>
public sealed record RecentSettings
{
    /// <summary>The most links kept; the oldest drop off first.</summary>
    public int MaxEntries { get; init; } = ShareDefaults.RecentMaxEntries;

    /// <summary>How many days an expired link stays in the list.</summary>
    public int KeepExpiredDays { get; init; } = ShareDefaults.RecentKeepExpiredDays;
}
