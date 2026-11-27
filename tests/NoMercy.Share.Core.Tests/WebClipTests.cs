using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Tests;

public sealed class WebClipTests
{
    [Fact]
    public void AFullPageIsTheWholeContent()
    {
        Assert.Equal(new WebClip(0, 0, 1280, 3000), WebClip.FullPage(1280, 3000, 16384));
    }

    [Fact]
    public void ATallPageIsClampedToTheMaximumHeight()
    {
        Assert.Equal(new WebClip(0, 0, 1280, 16384), WebClip.FullPage(1280, 40000, 16384));
    }

    [Fact]
    public void AnElementBoxGetsTheScrollOffsetAdded()
    {
        Assert.Equal(new WebClip(100, 1500, 300, 200), WebClip.Element(100, 200, 300, 200, 0, 1300, 16384));
        Assert.Equal(new WebClip(130, 1500, 300, 200), WebClip.Element(100, 200, 300, 200, 30, 1300, 16384));
    }

    [Fact]
    public void ATallElementIsClampedToo()
    {
        Assert.Equal(300, WebClip.Element(0, 0, 300, 5000, 0, 0, 300).Height);
    }

    [Fact]
    public void AnElementWithNoAreaIsEmpty()
    {
        Assert.True(WebClip.Element(10, 10, 0, 50, 0, 0, 16384).IsEmpty);
        Assert.False(WebClip.FullPage(1280, 3000, 16384).IsEmpty);
    }
}
