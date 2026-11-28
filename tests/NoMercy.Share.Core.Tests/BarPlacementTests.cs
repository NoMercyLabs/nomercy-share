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

    [Theory]
    [InlineData(660, 0, 2260, 0)]
    [InlineData(0, 976, 1920, 960)]
    [InlineData(1320, 488, 2600, 480)]
    public void ABarMovedToAnotherScreenKeepsItsPlaceOnTheScreen(double x, double y, double movedX, double movedY)
    {
        Assert.Equal(new BarPosition(movedX, movedY), BarPlacement.MoveTo(new BarPosition(x, y), 600, 64, Main, Second));
    }

    [Fact]
    public void ABarAsWideAsItsScreenStartsAtTheLeftEdgeOfTheNewOne()
    {
        Assert.Equal(new BarPosition(1920, 0), BarPlacement.MoveTo(new BarPosition(0, 0), 1920, 64, Main, Second));
    }
}
