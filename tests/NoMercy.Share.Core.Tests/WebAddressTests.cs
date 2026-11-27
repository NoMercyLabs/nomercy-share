using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Tests;

public sealed class WebAddressTests
{
    [Theory]
    [InlineData("https://example.test/page")]
    [InlineData("http://example.test/page?x=1")]
    [InlineData("  https://example.test/  ")]
    public void AnHttpOrHttpsAddressIsAccepted(string text)
    {
        Assert.True(WebAddress.TryParse(text, out Uri? address));
        Assert.Equal("example.test", address!.Host);
    }

    [Theory]
    [InlineData("file:///C:/made-up/page.html")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<p>hi</p>")]
    [InlineData("chrome://settings")]
    [InlineData("edge://version")]
    [InlineData("example.test")]
    [InlineData("https://")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsRefused(string? text)
    {
        Assert.False(WebAddress.TryParse(text, out _));
    }

    [Fact]
    public void OnlyHttpsCountsAsSecure()
    {
        Assert.True(WebAddress.TryParse("https://example.test", out Uri? secure) && WebAddress.IsHttps(secure!));
        Assert.True(WebAddress.TryParse("http://example.test", out Uri? plain) && !WebAddress.IsHttps(plain!));
    }
}
