namespace NoMercy.Share.Core.Sharing;

/// <summary>An extra place that receives the share link after an upload, for example a Discord channel. It never receives the file.</summary>
public interface ILinkDestination
{
    /// <summary>The name shown to the user, for example "Discord".</summary>
    string Name { get; }

    /// <summary>False while the destination is off or has nothing to send to; it is then skipped.</summary>
    bool IsConfigured { get; }

    /// <summary>Sends the link. Throws <see cref="LinkDestinationException"/> with a short reason for the user.</summary>
    Task SendAsync(ShareLink link, string contentType, CancellationToken cancellationToken = default);
}
