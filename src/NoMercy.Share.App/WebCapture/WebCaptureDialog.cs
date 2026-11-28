using System.Diagnostics;
using Avalonia.Threading;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.App;

/// <summary>Opens the capture window, with an https address from the clipboard filled in, and waits until it closes.</summary>
public sealed class WebCaptureDialog(Core.IClipboard clipboard, Func<WebCaptureSettings> settings, Core.IPointerMonitor? pointer = null)
    : IWebCaptureDialog
{
    public async Task<CaptureResult> RunAsync(IWebPageCapture capture, CancellationToken cancellationToken = default)
    {
        string? copied = await ReadClipboardAsync(cancellationToken);
        TaskCompletionSource<CaptureResult?> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WebCaptureWindow window = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            WebCaptureViewModel model = new(capture, settings(), copied);
            WebCaptureWindow opened = new(model);
            opened.Closed += (_, _) => closed.TrySetResult(model.Result);
            PointerScreen.CenterOnOpen(opened, pointer);
            opened.Show();
            return opened;
        });
        using CancellationTokenRegistration stop = cancellationToken.Register(() => Dispatcher.UIThread.Post(window.Close));
        return await closed.Task ?? throw new OperationCanceledException("The web page capture was cancelled.");
    }

    private async Task<string?> ReadClipboardAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await clipboard.GetTextAsync(cancellationToken);
        }
        catch (Exception error)
        {
            Trace.TraceWarning("Reading the clipboard for the web capture window failed: {0}", error.Message);
            return null; // the address field just starts empty
        }
    }
}
