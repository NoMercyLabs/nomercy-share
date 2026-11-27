using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace NoMercy.Share.App;

/// <summary>
/// The clipboard through Avalonia, so every desktop shares one implementation. It lives in the app rather than
/// the platform module because it needs a window of this UI toolkit.
/// </summary>
public sealed class AvaloniaClipboard(Func<TopLevel?> owner) : Core.IClipboard
{
    // The clipboard renders the picture on demand, so the bitmap must outlive the call; the next picture replaces it.
    private Bitmap? _image;

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (owner()?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    public async Task SetImageAsync(string pngFilePath, CancellationToken cancellationToken = default)
    {
        if (owner()?.Clipboard is { } clipboard)
        {
            Bitmap next = new(pngFilePath);
            await clipboard.SetBitmapAsync(next);
            _image?.Dispose();
            _image = next;
        }
    }
}
