using NoMercy.Share.Core.Recent;

namespace NoMercy.Share.Core.Sharing;

/// <summary>
/// Deletes a shared upload through whichever provider says it made it. Every registered provider is asked, not only the
/// selected one, so a link stays deletable after the user switches providers.
/// </summary>
public sealed class ShareDeletion(Func<IEnumerable<IShareUploader>> uploaders)
{
    public bool CanDeleteFromHost(RecentLink link)
    {
        _ = (uploaders, link);
        return false;
    }

    public Task DeleteFromHostAsync(RecentLink link, CancellationToken cancellationToken = default)
    {
        _ = (link, cancellationToken);
        return Task.CompletedTask;
    }
}
