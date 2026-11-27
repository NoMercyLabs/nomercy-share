namespace NoMercy.Share.Core.Capture;

/// <summary>Takes a still picture of the screen. Each platform has its own implementation.</summary>
public interface IScreenCapture
{
    /// <summary>The user drags a rectangle; the picture holds only that region.</summary>
    Task<CaptureResult> CaptureRegionAsync(CancellationToken cancellationToken = default);

    /// <summary>The user picks a window; the picture holds only that window.</summary>
    Task<CaptureResult> CaptureWindowAsync(CancellationToken cancellationToken = default);

    /// <summary>The picture holds the whole screen.</summary>
    Task<CaptureResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default);
}
