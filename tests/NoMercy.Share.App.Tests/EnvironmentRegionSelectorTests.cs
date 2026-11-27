using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App.Tests;

public sealed class EnvironmentRegionSelectorTests
{
    [Fact]
    public void FourWholeNumbersMakeARegion()
    {
        Assert.Equal(new ScreenRect(-1920, 40, 320, 240), EnvironmentRegionSelector.FromEnvironment("-1920, 40,320,240"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("1,2,three,4")]
    public void AnythingElseMeansNoFixedRegionSoTheOverlayIsUsed(string? value)
    {
        Assert.Null(EnvironmentRegionSelector.FromEnvironment(value));
    }

    [Fact]
    public async Task TheFixedSelectorAnswersWithItsRegionAtOnce()
    {
        EnvironmentRegionSelector selector = new(new ScreenRect(1, 2, 3, 4));

        ScreenRect? chosen = await selector.SelectAsync(new ScreenFrame(0, 0, 1, 1, new byte[4]));

        Assert.Equal(new ScreenRect(1, 2, 3, 4), chosen);
    }
}
