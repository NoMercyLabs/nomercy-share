namespace NoMercy.Share.Core.Capture;

/// <summary>A web page could not be captured. The message is a full sentence for the user; <see cref="Field"/> says which field it is about.</summary>
public sealed class WebCaptureException(WebCaptureField field, string message) : Exception(message)
{
    public WebCaptureField Field { get; } = field;
}
