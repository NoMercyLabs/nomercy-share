namespace NoMercy.Share.Core.Capture;

public enum WebCaptureKind
{
    FullPage,
    Viewport,
    Element
}

/// <summary>What to capture from a web page.</summary>
/// <param name="Page">The page address; null means the page the user has open in the browser.</param>
/// <param name="Kind">Full page, only the visible part, or one element.</param>
/// <param name="ElementSelector">A CSS selector, used only when <paramref name="Kind"/> is <see cref="WebCaptureKind.Element"/>.</param>
/// <param name="Device">The page width: desktop or phone.</param>
public sealed record WebCaptureRequest(
    Uri? Page = null,
    WebCaptureKind Kind = WebCaptureKind.FullPage,
    string? ElementSelector = null,
    WebCaptureDevice Device = WebCaptureDevice.Desktop);

/// <summary>Takes a picture of a web page. Each platform has its own implementation.</summary>
public interface IWebPageCapture
{
    Task<CaptureResult> CaptureAsync(WebCaptureRequest request, CancellationToken cancellationToken = default);
}
