using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Settings;

/// <summary>How long links live.</summary>
public sealed record ExpirySettings
{
    /// <summary>The default lifetime in days; null means links never expire.</summary>
    public int? DefaultDays { get; init; } = ShareDefaults.ExpiryDays;

    /// <summary>The expiry of an upload that names none.</summary>
    public Expiry Default => DefaultDays is { } days ? Expiry.InDays(days) : Expiry.Never;

    /// <summary>The expiry of one upload: its own choice when it has one, else the default.</summary>
    public Expiry Resolve(Expiry? perUpload) => perUpload ?? Default;
}
