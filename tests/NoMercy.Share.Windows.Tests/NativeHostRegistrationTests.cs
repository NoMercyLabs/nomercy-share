using System.Text.Json;
using Microsoft.Win32;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

public sealed class NativeHostRegistrationTests : IDisposable
{
    private const string HostName = "tv.nomercy.share";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-registration-{Guid.NewGuid():N}");
    private readonly string _softwareKey = $@"Software\NoMercyShareTest\{Guid.NewGuid():N}";

    public NativeHostRegistrationTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_softwareKey.Split('\\')[0] + @"\NoMercyShareTest", throwOnMissingSubKey: false);
        Directory.Delete(_folder, recursive: true);
    }

    private string ManifestPath => Path.Combine(_folder, "native-host", $"{HostName}.json");

    private NativeHostRegistration Registration(NativeHostSettings? settings = null) =>
        new(_folder, @"C:\Apps\Share\nomercy-share.exe", settings ?? new NativeHostSettings(), _softwareKey);

    private string? Default(string browserPath) =>
        Registry.CurrentUser.OpenSubKey($@"{_softwareKey}\{browserPath}\NativeMessagingHosts\{HostName}")?.GetValue(null) as string;

    [Fact]
    public void TheManifestNamesTheExeAndTheAllowedOrigin()
    {
        Assert.True(Registration().Register());

        JsonElement manifest = JsonDocument.Parse(File.ReadAllText(ManifestPath)).RootElement;
        Assert.Equal(HostName, manifest.GetProperty("name").GetString());
        Assert.Equal("NoMercy Share", manifest.GetProperty("description").GetString());
        Assert.Equal(@"C:\Apps\Share\nomercy-share.exe", manifest.GetProperty("path").GetString());
        Assert.Equal("stdio", manifest.GetProperty("type").GetString());
        Assert.Equal($"chrome-extension://{ShareDefaults.ExtensionId}/", manifest.GetProperty("allowed_origins")[0].GetString());
    }

    [Fact]
    public void EdgeAndChromeKeysPointAtTheManifest()
    {
        Registration().Register();

        Assert.Equal(ManifestPath, Default("Microsoft\\Edge"));
        Assert.Equal(ManifestPath, Default("Google\\Chrome"));
    }

    [Fact]
    public void ASecondRunWritesNothing()
    {
        Registration().Register();
        DateTime stamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(ManifestPath, stamp);

        Assert.False(Registration().Register());
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(ManifestPath));
    }

    [Fact]
    public void AChangedExeOrOriginIsWrittenAgain()
    {
        Registration().Register();

        Assert.True(new NativeHostRegistration(_folder, @"C:\Other\nomercy-share.exe", new NativeHostSettings(), _softwareKey).Register());
        Assert.True(Registration(new NativeHostSettings { AllowedOrigins = ["chrome-extension://bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/"] }).Register());
        Assert.Contains("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", File.ReadAllText(ManifestPath));
    }

    [Fact]
    public void ADeletedManifestIsWrittenAgain()
    {
        Registration().Register();
        File.Delete(ManifestPath);

        Assert.True(Registration().Register());
        Assert.True(File.Exists(ManifestPath));
    }

    [Fact]
    public void TheRegisterSettingDefaultsToOn()
    {
        Assert.True(new NativeHostSettings().Register);
    }
}
