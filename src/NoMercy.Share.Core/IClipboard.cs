namespace NoMercy.Share.Core;

/// <summary>The system clipboard. The app registers the implementation that fits its UI toolkit.</summary>
public interface IClipboard
{
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Puts the picture in a PNG file on the clipboard, so it can be pasted into a chat or an editor.</summary>
    Task SetImageAsync(string pngFilePath, CancellationToken cancellationToken = default);
}
