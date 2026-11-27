using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class SizeCapRetryTests
{
    private const long Megabyte = 1024 * 1024;

    private static readonly RecordingSettings Defaults = new();

    [Fact]
    public void AnOverSizeResultScalesTheBitrateByCapOverSizeTimesTheHeadroomAndKeepsThePictureSize()
    {
        VideoPlan first = new(1920, 1080, 3110);

        VideoPlan retry = VideoBudget.Retry(first, 100 * Megabyte, Defaults);

        Assert.Equal(new VideoPlan(1920, 1080, 1477), retry); // 3110 * (50 / 100) * 0.95
    }

    [Fact]
    public void TheRetryBitrateNeverDropsBelowOne()
    {
        VideoPlan retry = VideoBudget.Retry(new VideoPlan(2, 2, 1), 1000 * Megabyte, Defaults);

        Assert.Equal(1, retry.VideoKbps);
    }

    [Fact]
    public async Task AFileUnderTheCapIsEncodedOnceAndNeverRetried()
    {
        List<VideoPlan> plans = [];

        SqueezeOutcome outcome = await SizeCapRetry.RunAsync(new VideoPlan(1280, 720, 1000), Defaults, plan =>
        {
            plans.Add(plan);
            return Task.FromResult(10 * Megabyte);
        });

        Assert.Single(plans);
        Assert.Equal((0, false), (outcome.Retries, outcome.OverCap));
    }

    [Fact]
    public async Task AnOverSizeFileIsEncodedAgainWithTheScaledBitrateAndStopsOnceItFits()
    {
        List<VideoPlan> plans = [];
        long[] sizes = [80 * Megabyte, 40 * Megabyte];

        SqueezeOutcome outcome = await SizeCapRetry.RunAsync(new VideoPlan(1280, 720, 1000), Defaults, plan =>
        {
            plans.Add(plan);
            return Task.FromResult(sizes[plans.Count - 1]);
        });

        Assert.Equal([1000, 593], plans.Select(plan => plan.VideoKbps)); // 1000 * (50 / 80) * 0.95
        Assert.Equal((1, false, 40 * Megabyte), (outcome.Retries, outcome.OverCap, outcome.Bytes));
        Assert.Equal(593, outcome.Plan.VideoKbps);
    }

    [Fact]
    public async Task TheLoopStopsAtMaxCompressAttemptsAndReportsTheFileAsOverTheCap()
    {
        int calls = 0;

        SqueezeOutcome outcome = await SizeCapRetry.RunAsync(new VideoPlan(1280, 720, 1000), Defaults, _ =>
        {
            calls++;
            return Task.FromResult(200 * Megabyte);
        });

        Assert.Equal(3, calls); // the first pass plus the 2 retries of the default
        Assert.Equal((2, true, 200 * Megabyte), (outcome.Retries, outcome.OverCap, outcome.Bytes));
    }

    [Fact]
    public async Task ZeroAttemptsMeansNoRetryAtAll()
    {
        int calls = 0;

        SqueezeOutcome outcome = await SizeCapRetry.RunAsync(new VideoPlan(1280, 720, 1000), new RecordingSettings { MaxCompressAttempts = 0 }, _ =>
        {
            calls++;
            return Task.FromResult(200 * Megabyte);
        });

        Assert.Equal(1, calls);
        Assert.True(outcome.OverCap);
    }

    [Fact]
    public void TheLimitsAreSettingsWithTheirDefaults()
    {
        RecordingSettings recording = new ShareSettings().Recording;

        Assert.Equal(2, recording.MaxCompressAttempts);
        Assert.Equal(1000, recording.StartupCheckMilliseconds);
    }
}
