using System.Globalization;

namespace NoMercy.Share.Core.Recent;

/// <summary>One line of the recent list: a stored link and the text made from it for one moment, in invariant English.</summary>
public sealed class RecentRow(RecentLink link, DateTimeOffset now)
{
    public RecentLink Link { get; } = link;

    /// <summary>The key part of the address, without the host: exp-30d/abc.png.</summary>
    public string PathText { get; } = Uri.TryCreate(link.Url, UriKind.Absolute, out Uri? url) ? url.AbsolutePath.TrimStart('/') : link.Url;

    public string AgeText { get; } = Age(now - link.CreatedAt);

    /// <summary>"expires in 29 days", "expired", or null when the link never expires.</summary>
    public string? ExpiryText { get; } = link.ExpiresAt is { } expiresAt ? Expiry(expiresAt - now) : null;

    /// <summary>The age and the expiry on one line: "5 min ago · expires in 29 days".</summary>
    public string DetailText => ExpiryText is null ? AgeText : $"{AgeText} · {ExpiryText}";

    public bool IsExpired { get; } = link.IsExpired(now);

    public bool IsVideo => Link.Kind == RecentKind.Video;

    /// <summary>The Moooom icon shown where there is no thumbnail.</summary>
    public string IconName => IsVideo ? "filmMedia" : "picture";

    /// <summary>The local picture, only while the file is still there; null for a video.</summary>
    public string? ThumbnailPath { get; } = link.Kind == RecentKind.Image && link.FilePath is { } file && File.Exists(file) ? file : null;

    private static string Age(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => Strings.Get("recent.age.now"),
        < 60 => Strings.Get("recent.age.minutes", Whole(age.TotalMinutes)),
        < 60 * 24 => Strings.Get("recent.age.hours", Whole(age.TotalHours)),
        _ => Strings.Get("recent.age.days", Whole(age.TotalDays))
    };

    private static string Expiry(TimeSpan left)
    {
        if (left <= TimeSpan.Zero)
        {
            return Strings.Get("recent.expired");
        }

        return left.TotalHours switch
        {
            < 1 => Strings.Get("recent.expires.minutes", Whole(Math.Max(1, left.TotalMinutes))),
            < 24 => Strings.Get("recent.expires.hours", Whole(left.TotalHours)),
            < 48 => Strings.Get("recent.expires.day"),
            _ => Strings.Get("recent.expires.days", Whole(left.TotalDays))
        };
    }

    private static string Whole(double value) => ((int)value).ToString(CultureInfo.InvariantCulture);
}
