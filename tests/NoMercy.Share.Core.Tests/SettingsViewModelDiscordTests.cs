using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class SettingsViewModelDiscordTests : IDisposable
{
    private const string Webhook = "https://discord.com/api/webhooks/000/made-up-token";
    private const string Stored = "https://discord.com/api/webhooks/111/stored-token";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-discord-settings-test-{Guid.NewGuid():N}");
    private readonly FakeCredentialStore _credentials = new();
    private readonly SettingsFile _file;
    private readonly LiveSettings _live;
    private readonly HotkeyBinder _binder;

    public SettingsViewModelDiscordTests()
    {
        _file = new SettingsFile(SettingsFile.DefaultPath(_folder));
        _live = new LiveSettings(_file.Load());
        BarViewModel bar = new(
            new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
            new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()));
        _binder = new HotkeyBinder(new FakeGlobalHotkeys(), bar, _live.Current.Hotkeys);
        _binder.Start();
    }

    private SettingsViewModel Open() => new(_file, _live, _credentials, _binder, ["r2"]);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void ItLoadsTheSwitchAndNeverTheStoredWebhook()
    {
        _file.Save(new ShareSettings { Discord = new DiscordSettings { Enabled = true } });
        _credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", Stored);

        SettingsViewModel model = Open();

        Assert.True(model.DiscordEnabled);
        Assert.Equal(string.Empty, model.DiscordWebhook);
        Assert.Equal("Saved", model.DiscordWebhookPlaceholder);
    }

    [Fact]
    public void WithNoWebhookStoredThePlaceholderIsEmpty()
    {
        SettingsViewModel model = Open();

        Assert.False(model.DiscordEnabled);
        Assert.Equal(string.Empty, model.DiscordWebhookPlaceholder);
    }

    [Fact]
    public void SaveWritesTheWebhookToTheCredentialStoreAndOnlyTheSwitchToSettings()
    {
        SettingsViewModel model = Open();
        model.DiscordEnabled = true;
        model.DiscordWebhook = $"  {Webhook}  ";

        model.Save();

        Assert.True(model.Saved);
        (string name, StoredCredential credential) = Assert.Single(_credentials.Writes);
        Assert.Equal("nomercy-share-discord", name);
        Assert.Equal(Webhook, credential.SecretAccessKey);
        Assert.True(_file.Load().Discord.Enabled);
        Assert.DoesNotContain("made-up-token", File.ReadAllText(SettingsFile.DefaultPath(_folder)), StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAppliesTheSwitchToTheLiveSettings()
    {
        SettingsViewModel model = Open();
        model.DiscordEnabled = true;
        model.DiscordWebhook = Webhook;

        model.Save();

        Assert.True(_live.Current.Discord.Enabled);
    }

    [Fact]
    public void AnEmptyBoxKeepsTheStoredWebhook()
    {
        _credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", Stored);
        SettingsViewModel model = Open();
        model.DiscordEnabled = true;

        model.Save();

        Assert.True(model.Saved);
        Assert.Empty(_credentials.Writes);
        Assert.Equal(Stored, _credentials.Stored[ShareDefaults.DiscordCredentialName].SecretAccessKey);
    }

    [Fact]
    public void TheSwitchOnWithNoWebhookAnywhereShowsTheErrorOnThatField()
    {
        SettingsViewModel model = Open();
        model.DiscordEnabled = true;

        model.Save();

        Assert.False(model.Saved);
        Assert.Equal("Enter a Discord webhook address.", model.DiscordWebhookError);
        Assert.Empty(_credentials.Writes);
    }

    [Fact]
    public void AWebhookThatIsNotDiscordsShowsTheErrorAndWritesNothing()
    {
        SettingsViewModel model = Open();
        model.DiscordEnabled = true;
        model.DiscordWebhook = "https://example.test/api/webhooks/000/made-up-token";

        model.Save();

        Assert.False(model.Saved);
        Assert.Equal("Enter a full Discord webhook address, like https://discord.com/api/webhooks/….", model.DiscordWebhookError);
        Assert.DoesNotContain("made-up-token", model.DiscordWebhookError, StringComparison.Ordinal);
        Assert.Empty(_credentials.Writes);
    }

    [Fact]
    public void TheSwitchOffNeedsNoWebhook()
    {
        SettingsViewModel model = Open();

        model.Save();

        Assert.True(model.Saved);
        Assert.Null(model.DiscordWebhookError);
        Assert.False(_file.Load().Discord.Enabled);
    }

    [Fact]
    public void TheCredentialNameInTheSettingsIsTheOneUsed()
    {
        _file.Save(new ShareSettings { Discord = new DiscordSettings { CredentialName = "my-discord" } });
        SettingsViewModel model = Open();
        model.DiscordEnabled = true;
        model.DiscordWebhook = Webhook;

        model.Save();

        Assert.Equal("my-discord", Assert.Single(_credentials.Writes).Name);
    }

    [Fact]
    public void SavingKeepsTheTimeoutTheUserSetInTheFile()
    {
        _file.Save(new ShareSettings { Discord = new DiscordSettings { TimeoutSeconds = 25 } });
        SettingsViewModel model = Open();

        model.Save();

        Assert.Equal(25, _file.Load().Discord.TimeoutSeconds);
    }
}
