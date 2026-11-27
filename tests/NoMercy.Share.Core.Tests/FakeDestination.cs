using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

internal sealed class FakeDestination(string name = "Discord") : ILinkDestination
{
    public string Name { get; } = name;

    public bool IsConfigured { get; set; } = true;

    public Exception? Throws { get; set; }

    public List<(ShareLink Link, string ContentType)> Sent { get; } = [];

    /// <summary>Runs inside every send, so a test can look at what the clipboard held at that moment.</summary>
    public Action? OnSend { get; set; }

    public Task SendAsync(ShareLink link, string contentType, CancellationToken cancellationToken = default)
    {
        OnSend?.Invoke();
        if (Throws is not null)
        {
            return Task.FromException(Throws);
        }

        Sent.Add((link, contentType));
        return Task.CompletedTask;
    }
}
