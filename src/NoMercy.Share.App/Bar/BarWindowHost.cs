using Avalonia.Threading;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>Hides the bar while a picture is taken, so it never appears in it, and shows it again after.</summary>
public sealed class BarWindowHost(Func<BarWindow?> owner) : ICaptureHost
{
    public async Task HideAsync(CancellationToken cancellationToken = default) =>
        await Dispatcher.UIThread.InvokeAsync(() => owner()?.Hide());

    public async Task ShowAsync(CancellationToken cancellationToken = default) =>
        await Dispatcher.UIThread.InvokeAsync(() => owner()?.Show());
}
