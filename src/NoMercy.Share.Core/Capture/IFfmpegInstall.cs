namespace NoMercy.Share.Core.Capture;

/// <summary>Whether ffmpeg is on this PC, and the download of the NoMercy build when it is not.</summary>
public interface IFfmpegInstall
{
    bool IsFound();

    /// <summary>Downloads, checks and unpacks ffmpeg; progress goes from 0 to 1. Throws when it cannot, with a reason a person can read.</summary>
    Task DownloadAsync(IProgress<double> progress, CancellationToken cancellationToken = default);
}
