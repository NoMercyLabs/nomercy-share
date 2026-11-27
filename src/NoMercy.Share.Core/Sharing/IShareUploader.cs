namespace NoMercy.Share.Core.Sharing;

/// <summary>The public link of an upload.</summary>
/// <param name="Url">The address to share.</param>
/// <param name="ExpiresAt">When the link stops working (UTC); null when it never does.</param>
public sealed record ShareLink(Uri Url, DateTimeOffset? ExpiresAt);

/// <summary>
/// Puts a file on a share host. A provider (R2, another S3 host, ...) implements this and is picked by
/// <see cref="ProviderName"/> from the storage settings.
/// </summary>
public interface IShareUploader
{
    /// <summary>The name the storage settings use to select this provider, for example "r2".</summary>
    string ProviderName { get; }

    /// <summary>Throws <see cref="InvalidStorageSettingsException"/> naming the setting that cannot work. The share flow calls it before each upload.</summary>
    void Validate()
    {
    }

    Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default);
}
