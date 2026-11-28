namespace NoMercy.Share.Core.Sharing;

/// <summary>The public link of an upload.</summary>
/// <param name="Url">The address to share.</param>
/// <param name="ExpiresAt">When the link stops working (UTC); null when it never does or the host does not say.</param>
/// <param name="HostId">The host's own id of the upload, for a host that needs it to delete; otherwise null.</param>
public sealed record ShareLink(Uri Url, DateTimeOffset? ExpiresAt, string? HostId = null);
