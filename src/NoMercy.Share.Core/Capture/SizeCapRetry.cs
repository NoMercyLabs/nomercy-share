using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Capture;

/// <summary>Bamo's retry idea: a video that is still over the size cap is encoded again at a lower bitrate, up to a limit from the settings.</summary>
public static class SizeCapRetry
{
    /// <summary>Encodes the first plan, then retries while the file is over the cap. <paramref name="encodeAsync"/> writes the file and returns its size in bytes.</summary>
    public static async Task<SqueezeOutcome> RunAsync(VideoPlan first, RecordingSettings settings, Func<VideoPlan, Task<long>> encodeAsync)
    {
        double capBytes = settings.MaxSizeMegabytes * 1024 * 1024;
        VideoPlan plan = first;
        long bytes = await encodeAsync(plan);
        int retries = 0;
        while (bytes > capBytes && retries < settings.MaxCompressAttempts)
        {
            plan = VideoBudget.Retry(plan, bytes, settings);
            bytes = await encodeAsync(plan);
            retries++;
        }

        return new SqueezeOutcome(plan, bytes, retries, bytes > capBytes);
    }
}
