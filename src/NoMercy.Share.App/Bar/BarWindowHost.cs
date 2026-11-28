using Avalonia.Threading;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>Hides the bar while a picture is taken, so it never appears in it, and puts it back after only when it was on screen.</summary>
public sealed class BarWindowHost(Func<BarWindow?> owner) : ICaptureHost
{
    private bool _wasShown;

    public async Task HideAsync(CancellationToken cancellationToken = default) =>
        await Dispatcher.UIThread.InvokeAsync(() => _wasShown = owner()?.HideForCapture() ?? false);

    public async Task ShowAsync(CancellationToken cancellationToken = default) =>
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_wasShown)
            {
                owner()?.Show();
            }
        });
}
