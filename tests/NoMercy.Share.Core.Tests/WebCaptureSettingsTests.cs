using System.Text.Json;
using NoMercy.Share.Core.Capture;
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

    [Theory]
    [InlineData("TabletWidth", 834)]
    [InlineData("FullHdWidth", 1920)]
    [InlineData("QuadHdWidth", 2560)]
    [InlineData("UltraHdWidth", 3840)]
    public void EveryAddedWidthHasItsDefault(string setting, int width)
    {
        JsonElement written = JsonSerializer.SerializeToElement(new WebCaptureSettings());

        Assert.True(written.TryGetProperty(setting, out JsonElement value), $"{setting} is not a setting");
        Assert.Equal(width, value.GetInt32());
    }

    [Fact]
    public void AnOldSettingsFileKeepsItsWidthsAndGetsTheDefaultsForTheNewOnes()
    {
        WebCaptureSettings loaded = Load("""{ "WebCapture": { "DesktopWidth": 1366, "PhoneWidth": 412 } }""");

        Assert.Equal([412, 834, 1366, 1920, 2560, 3840], Widths(loaded));
    }

    [Fact]
    public void AChangedAddedWidthReachesTheChoices()
    {
        WebCaptureSettings loaded = Load("""{ "WebCapture": { "TabletWidth": 800, "FullHdWidth": 1800, "QuadHdWidth": 2400, "UltraHdWidth": 3200 } }""");

        Assert.Equal([390, 800, 1280, 1800, 2400, 3200], Widths(loaded));
    }

    private WebCaptureSettings Load(string json)
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, json);
        return new SettingsFile(path).Load().WebCapture;
    }

    private static IEnumerable<int> Widths(WebCaptureSettings settings) =>
        new WebCaptureViewModel(new FakeWebPageCapture(), settings, null).Devices.Select(device => device.Width);

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
