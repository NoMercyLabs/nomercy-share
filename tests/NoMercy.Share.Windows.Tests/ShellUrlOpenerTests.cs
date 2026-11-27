using System.Diagnostics;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

public sealed class ShellUrlOpenerTests
{
    private readonly List<ProcessStartInfo> _started = [];

    private ShellUrlOpener Opener() => new(_started.Add);

    [Fact]
    public void AnHttpsAddressIsHandedToTheShell()
    {
        Opener().Open(new Uri("https://share.example.test/exp-30d/abc.png"));

        ProcessStartInfo start = Assert.Single(_started);
        Assert.Equal("https://share.example.test/exp-30d/abc.png", start.FileName);
        Assert.True(start.UseShellExecute);
    }

    [Theory]
    [InlineData("http://share.example.test/abc.png")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ftp://share.example.test/abc.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:someone@example.test")]
    [InlineData("ms-settings:network")]
    public void AnythingThatIsNotHttpsIsRefusedAndNothingStarts(string address)
    {
        Assert.Throws<ArgumentException>(() => Opener().Open(new Uri(address)));

        Assert.Empty(_started);
    }

    [Fact]
    public void ARelativeAddressIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Opener().Open(new Uri("abc.png", UriKind.Relative)));

        Assert.Empty(_started);
    }
}
