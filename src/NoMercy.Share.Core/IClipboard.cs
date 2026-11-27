namespace NoMercy.Share.Core;

/// <summary>The system clipboard. The app registers the implementation that fits its UI toolkit.</summary>
public interface IClipboard
{
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}
