namespace NoMercy.Share.Core.Capture;

/// <summary>Asks the user what to capture, runs the capture and returns its picture. The UI toolkit implements it.</summary>
public interface IWebCaptureDialog
{
    /// <summary>Throws <see cref="OperationCanceledException"/> when the user cancels.</summary>
    Task<CaptureResult> RunAsync(IWebPageCapture capture, CancellationToken cancellationToken = default);
}
