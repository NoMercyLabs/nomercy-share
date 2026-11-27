namespace NoMercy.Share.Core.Settings;

/// <summary>Where the bar was last seen, in screen pixels.</summary>
public sealed record BarPosition(double X, double Y);

public sealed record BarSettings
{
    public BarPosition? Position { get; init; }

    public int NoticeSeconds { get; init; } = ShareDefaults.NoticeSeconds;

    public double FirstOpenTopMargin { get; init; } = ShareDefaults.FirstOpenTopMargin;
}

/// <summary>Everything the user can set, as one file.</summary>
public sealed record ShareSettings
{
    public StorageSettings Storage { get; init; } = new();

    public ExpirySettings Expiry { get; init; } = new();

    public BarSettings Bar { get; init; } = new();
}
