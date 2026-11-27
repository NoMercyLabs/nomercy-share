using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class WebCaptureSettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-web-settings-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheDefaultsAreTheNamedValues()
    {
        WebCaptureSettings settings = new ShareSettings().WebCapture;

        Assert.Equal(string.Empty, settings.BrowserPath);
        Assert.Equal(1280, settings.DesktopWidth);
        Assert.Equal(390, settings.PhoneWidth);
        Assert.Equal(800, settings.Height);
        Assert.Equal(2, settings.ScaleFactor);
        Assert.Equal(30, settings.LoadTimeoutSeconds);
        Assert.Equal(500, settings.SettleMilliseconds);
        Assert.Equal(16384, settings.MaxFullPageHeight);
    }

    [Fact]
    public void AChangedValueSurvivesTheSettingsFile()
    {
        SettingsFile file = new(Path.Combine(_folder, "settings.json"));
        file.Save(new ShareSettings { WebCapture = new WebCaptureSettings { PhoneWidth = 412, MaxFullPageHeight = 9000, BrowserPath = "C:/made-up/browser.exe" } });

        WebCaptureSettings loaded = file.Load().WebCapture;

        Assert.Equal(412, loaded.PhoneWidth);
        Assert.Equal(9000, loaded.MaxFullPageHeight);
        Assert.Equal("C:/made-up/browser.exe", loaded.BrowserPath);
        Assert.Equal(1280, loaded.DesktopWidth);
    }
}
