using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class VideoBudgetTests
{
    private static readonly RecordingSettings Defaults = new();

    [Fact]
    public void AShortClipIsHeldDownByTheBitsPerPixelCapNotBySizeSoItStaysAtSourceSize()
    {
        VideoPlan plan = VideoBudget.Plan(10, 1920, 1080, Defaults);

        Assert.Equal((1920, 1080), (plan.Width, plan.Height));
        Assert.Equal(3110, plan.VideoKbps); // 0.05 bits * 1920 * 1080 * 30 fps / 1000
    }

    [Fact]
    public void ALongClipIsHeldDownBySizeSoTheBitrateIsTheSizeBudgetAndThePictureShrinks()
    {
        VideoPlan plan = VideoBudget.Plan(600, 1920, 1080, Defaults);

        // The size budget is 50 MB * 0.95 * 8 * 1024 / 600 s = 648 kbps, which buys sqrt(648000 / (0.05 * 30)) pixels of the same shape.
        Assert.Equal((876, 492), (plan.Width, plan.Height));
        Assert.Equal(646, plan.VideoKbps); // the even-sized picture needs 0.05 * 876 * 492 * 30 / 1000, a little under the budget
    }

    [Fact]
    public void ALongClipStaysUnderTheSizeCap()
    {
        VideoPlan plan = VideoBudget.Plan(600, 1920, 1080, Defaults);

        double megabytes = plan.VideoKbps * 600 / 8 / 1024;
        Assert.True(megabytes <= Defaults.MaxSizeMegabytes * Defaults.SizeHeadroom, $"{megabytes} MB");
    }

    [Fact]
    public void ABiggerSourceIsScaledDownToTheMaximumKeepingItsShape()
    {
        VideoPlan plan = VideoBudget.Plan(10, 3840, 2160, Defaults);

        Assert.Equal((1920, 1080), (plan.Width, plan.Height));
    }

    [Fact]
    public void ASourceWiderThanTallIsFittedInsideBothMaximums()
    {
        VideoPlan plan = VideoBudget.Plan(10, 3000, 1000, Defaults);

        Assert.Equal((1920, 640), (plan.Width, plan.Height));
    }

    [Fact]
    public void OddSizesBecomeEvenBecauseH264NeedsThem()
    {
        VideoPlan plan = VideoBudget.Plan(10, 1001, 555, Defaults);

        Assert.Equal((1000, 554), (plan.Width, plan.Height));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.001)]
    [InlineData(-5)]
    public void ANoDurationClipGetsTheBitsPerPixelCapAndNoCrash(double seconds)
    {
        VideoPlan plan = VideoBudget.Plan(seconds, 1280, 720, Defaults);

        Assert.Equal((1280, 720), (plan.Width, plan.Height));
        Assert.Equal(1382, plan.VideoKbps); // 0.05 * 1280 * 720 * 30 / 1000
    }

    [Fact]
    public void AudioTakesItsBitrateOutOfTheSizeBudget()
    {
        VideoPlan plan = VideoBudget.Plan(600, 1920, 1080, Defaults, includeAudio: true);

        Assert.InRange(plan.VideoKbps, 500, 520); // the size budget is 648 - 128 = 520
    }

    [Fact]
    public void ABiggerAudioBitrateLeavesLessForTheVideoByTheSameAmount()
    {
        RecordingSettings quiet = Defaults with { AudioBitrateKbps = 64 };
        RecordingSettings loud = Defaults with { AudioBitrateKbps = 192 };

        int withLess = VideoBudget.Plan(900, 1920, 1080, quiet, includeAudio: true).VideoKbps;
        int withMore = VideoBudget.Plan(900, 1920, 1080, loud, includeAudio: true).VideoKbps;

        Assert.InRange(withLess - withMore, 127, 128); // the budget is cut to whole kilobits, so it can be one short
    }

    [Fact]
    public void ChangingASettingChangesThePlan()
    {
        RecordingSettings small = Defaults with { MaxSizeMegabytes = 10, TargetBitsPerPixel = 0.1, FrameRate = 60, MaxWidth = 1280, MaxHeight = 720 };

        VideoPlan plan = VideoBudget.Plan(10, 1920, 1080, small);

        Assert.Equal((1280, 720), (plan.Width, plan.Height));
        Assert.Equal(5529, plan.VideoKbps); // 0.1 * 1280 * 720 * 60 / 1000; the size budget (7782) is larger
    }
}
