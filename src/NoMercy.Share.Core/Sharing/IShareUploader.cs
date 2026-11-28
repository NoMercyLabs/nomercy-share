using NoMercy.Share.Core.Recent;

namespace NoMercy.Share.Core.Sharing;

/// <summary>Puts a file on a share host. A provider (R2, another S3 host, ...) implements this and is picked by <see cref="ProviderName"/>.</summary>
public interface IShareUploader
{
    /// <summary>The name the storage settings use to select this provider, for example "r2".</summary>
    string ProviderName { get; }

    /// <summary>False while no share host is set up at all; captures then stay local and the image goes on the clipboard.</summary>
    bool IsConfigured { get; }

    /// <summary>Uploads the file. Throws <see cref="InvalidStorageSettingsException"/> naming a setting that cannot work.</summary>
    Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default);

    /// <summary>True when this provider can delete the upload behind this link from the host.</summary>
    bool CanDelete(RecentLink link);

    /// <summary>Deletes an upload <see cref="CanDelete"/> answered true for. Throws <see cref="ShareUploadException"/> when the host did not delete it.</summary>
    Task DeleteAsync(RecentLink link, CancellationToken cancellationToken = default);
}
