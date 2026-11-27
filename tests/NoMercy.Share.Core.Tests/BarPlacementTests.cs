using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class BarPlacementTests
{
    private static readonly ScreenArea Main = new(0, 0, 1920, 1040);
    private static readonly ScreenArea Second = new(1920, 0, 1280, 1024);

    [Fact]
    public void APositionOnAScreenStays()
    {
        Assert.Equal(new BarPosition(100, 50), BarPlacement.Clamp(new BarPosition(100, 50), 600, 64, [Main]));
    }

    [Fact]
    public void ABarOffTheRightEdgeIsPulledBackIn()
    {
        Assert.Equal(new BarPosition(1320, 50), BarPlacement.Clamp(new BarPosition(5000, 50), 600, 64, [Main]));
    }

    [Fact]
    public void ABarOnAScreenThatIsGoneMovesToTheNearestScreen()
    {
        BarPosition placed = BarPlacement.Clamp(new BarPosition(4000, 200), 600, 64, [Main, Second]);

        Assert.Equal(new BarPosition(2600, 200), placed);
    }

    [Fact]
    public void ANegativePositionOnASecondScreenLeftOfTheMainOneStays()
    {
        ScreenArea left = new(-1280, 0, 1280, 1024);

        Assert.Equal(new BarPosition(-900, 10), BarPlacement.Clamp(new BarPosition(-900, 10), 600, 64, [left, Main]));
    }

    [Fact]
    public void WithNoScreenTheWantedPositionIsKept()
    {
        Assert.Equal(new BarPosition(7, 8), BarPlacement.Clamp(new BarPosition(7, 8), 600, 64, []));
    }

    [Fact]
    public void TheFirstOpenIsCenteredAtTheTopBelowTheMargin()
    {
        Assert.Equal(new BarPosition(660, 24), BarPlacement.FirstOpen(Main, 600, 24));
    }
}
