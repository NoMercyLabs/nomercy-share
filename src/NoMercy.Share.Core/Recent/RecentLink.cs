namespace NoMercy.Share.Core.Recent;

/// <summary>One shared link as recent.json keeps it. Times are UTC and the size is in bytes; the list makes the text from them.</summary>
/// <param name="Url">The public link.</param>
/// <param name="ContentType">The media type of the upload, for example image/png.</param>
/// <param name="ExpiresAt">When the link stops working; null when it never does.</param>
/// <param name="FilePath">The local capture, kept only while the file exists when the link is recorded.</param>
public sealed record RecentLink(
    string Url,
    RecentKind Kind,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    string? FilePath)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiresAt && now >= expiresAt;
}
