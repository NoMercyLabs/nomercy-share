namespace NoMercy.Share.Core.Sharing;

/// <summary>How long an upload stays available. A null lifetime means it never expires.</summary>
public readonly record struct Expiry(TimeSpan? Lifetime)
{
    public static Expiry Never => new(null);

    public static Expiry After(TimeSpan lifetime) => new(lifetime);

    public static Expiry InDays(int days) => new(TimeSpan.FromDays(days));

    public bool IsNever => Lifetime is null;
}
