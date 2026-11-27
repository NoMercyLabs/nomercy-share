using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace NoMercy.Share.App;

/// <summary>
/// The clipboard through Avalonia, so every desktop shares one implementation. It lives in the app rather than
/// the platform module because it needs a window of this UI toolkit.
/// </summary>
public sealed class AvaloniaClipboard(Func<TopLevel?> owner) : Core.IClipboard
{
    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (owner()?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
