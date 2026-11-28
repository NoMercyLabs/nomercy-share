using NoMercy.Share.Core.Recent;

namespace NoMercy.Share.Core.Sharing;

/// <summary>
/// Deletes a shared upload through whichever provider says it made it. Every registered provider is asked, not only the
/// selected one, so a link stays deletable after the user switches providers.
/// </summary>
public sealed class ShareDeletion(Func<IEnumerable<IShareUploader>> uploaders)
{
    public bool CanDeleteFromHost(RecentLink link) => Owner(link) is not null;

    /// <summary>Deletes a link <see cref="CanDeleteFromHost"/> answered true for. Throws <see cref="ShareUploadException"/> when the host did not delete it.</summary>
    public Task DeleteFromHostAsync(RecentLink link, CancellationToken cancellationToken = default) =>
        Owner(link) is { } owner
            ? owner.DeleteAsync(link, cancellationToken)
            : throw new InvalidOperationException("No provider can delete this link.");

    private IShareUploader? Owner(RecentLink link) => uploaders().FirstOrDefault(uploader => uploader.CanDelete(link));
}
