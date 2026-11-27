namespace NoMercy.Share.Storage.S3;

/// <summary>What the sidecar file next to an object says, for a host script that acts on expiry.</summary>
public sealed record ShareSidecar(string Key, string UploadedAt, string? ExpiresAt, string ContentType, long Bytes);
