using System.Collections.Concurrent;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// Tests each GPU encoder family with a real encode of a few black frames, using the same options a recording uses.
/// An encoder is offered only when that encode exits cleanly. Answers are kept per codec for the life of the app.
/// </summary>
public sealed class FfmpegEncoderProbe(Func<RecordingSettings> current) : IEncoderProbe
{
    /// <summary>A driver that hangs must not hold the settings window: a probe that runs longer counts as failed.</summary>
    private static readonly TimeSpan ProbeLimit = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<string>>> _answers = new();

    public Task<IReadOnlyList<string>> WorkingFamiliesAsync(string codec, CancellationToken cancellationToken = default) =>
        _answers.GetOrAdd(codec, ProbeAsync);

    private async Task<IReadOnlyList<string>> ProbeAsync(string codec)
    {
        RecordingSettings settings = current();
        if (FfmpegLocator.Find(settings.FfmpegPath, Environment.GetEnvironmentVariable("PATH")) is not { } ffmpeg)
        {
            return [VideoEncoder.Cpu];
        }

        string[] families = [.. VideoEncoder.Families];
        bool[] works = await Task.WhenAll(families.Select(family => WorksAsync(ffmpeg, new VideoEncoder(codec, family, ShareDefaults.RecordingSpeed))));
        return [.. families.Where((_, index) => works[index])];
    }

    private static async Task<bool> WorksAsync(string ffmpeg, VideoEncoder encoder)
    {
        try
        {
            using FfmpegProcess probe = FfmpegProcess.Start(ffmpeg, FfmpegArguments.Probe(encoder));
            return await probe.WaitAsync(ProbeLimit, CancellationToken.None) == 0;
        }
        catch (Exception error) when (error is RecordingException or IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
