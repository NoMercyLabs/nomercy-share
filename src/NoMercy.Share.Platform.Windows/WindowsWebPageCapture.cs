using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Web page pictures on Windows. Not built yet: the web capture step fills this in.</summary>
public sealed class WindowsWebPageCapture : IWebPageCapture
{
    public Task<CaptureResult> CaptureAsync(WebCaptureRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Windows web page capture arrives in the web capture step.");
}
