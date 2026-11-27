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

    private SettingsViewModel Open() => new(_file, _live, _credentials, _binder, ["r2", "s3", "discord"]);

    private static ShareSettings S3SetUp() => new()
    {
        Storage = new StorageSettings
        {
            Provider = "r2",
            Endpoint = "https://storage.example.test",
            Bucket = "made-up-bucket",
            PublicBaseUrl = "https://share.example.test",
            CredentialName = "stored-name"
        }
    };

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheProviderListHoldsDiscordWithItsOwnLabel()
    {
        SettingsViewModel model = Open();

        Assert.Equal(["Cloudflare R2", "S3-compatible", "Discord"], model.Providers.Select(choice => choice.Label));
    }

    [Fact]
    public void PickingDiscordShowsOnlyTheWebhookField()
    {
        SettingsViewModel model = Open();
        Assert.True(model.ShowsStorageFields);
        Assert.False(model.IsDiscord);

        model.Provider = "discord";

        Assert.True(model.IsDiscord);
        Assert.False(model.ShowsStorageFields);
    }

    [Fact]
    public void ItLoadsTheProviderAndNeverTheStoredWebhook()
    {
        _file.Save(new ShareSettings { Storage = new StorageSettings { Provider = "discord" } });
        _credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", Stored);

        SettingsViewModel model = Open();

        Assert.True(model.IsDiscord);
        Assert.Equal(string.Empty, model.DiscordWebhook);
        Assert.Equal("Saved", model.DiscordWebhookPlaceholder);
    }

    [Fact]
    public void WithNoWebhookStoredThePlaceholderIsEmpty()
    {
        Assert.Equal(string.Empty, Open().DiscordWebhookPlaceholder);
    }

    [Fact]
    public void SaveWritesTheWebhookToTheCredentialStoreAndNeverToSettings()
    {
        SettingsViewModel model = Open();
        model.Provider = "discord";
        model.DiscordWebhook = $"  {Webhook}  ";

        model.Save();

        Assert.True(model.Saved);
        (string name, StoredCredential credential) = Assert.Single(_credentials.Writes);
        Assert.Equal("nomercy-share-discord", name);
        Assert.Equal(Webhook, credential.SecretAccessKey);
        Assert.Equal("discord", _file.Load().Storage.Provider);
        Assert.DoesNotContain("made-up-token", File.ReadAllText(SettingsFile.DefaultPath(_folder)), StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAppliesTheProviderToTheLiveSettings()
    {
        SettingsViewModel model = Open();
        model.Provider = "discord";
        model.DiscordWebhook = Webhook;

        model.Save();

        Assert.Equal("discord", _live.Current.Storage.Provider);
    }

    [Fact]
    public void AnEmptyBoxKeepsTheStoredWebhook()
    {
        _credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", Stored);
        SettingsViewModel model = Open();
        model.Provider = "discord";

        model.Save();

        Assert.True(model.Saved);
        Assert.Empty(_credentials.Writes);
        Assert.Equal(Stored, _credentials.Stored[ShareDefaults.DiscordCredentialName].SecretAccessKey);
    }

    [Fact]
    public void DiscordWithNoWebhookAnywhereShowsTheErrorOnThatField()
    {
        SettingsViewModel model = Open();
        model.Provider = "discord";

        model.Save();

        Assert.False(model.Saved);
        Assert.Equal("Enter a Discord webhook address.", model.DiscordWebhookError);
        Assert.Empty(_credentials.Writes);
    }

    [Fact]
    public void AWebhookThatIsNotDiscordsShowsTheErrorAndWritesNothing()
    {
        SettingsViewModel model = Open();
        model.Provider = "discord";
        model.DiscordWebhook = "https://example.test/api/webhooks/000/made-up-token";

        model.Save();

        Assert.False(model.Saved);
        Assert.Equal("Enter a full Discord webhook address, like https://discord.com/api/webhooks/….", model.DiscordWebhookError);
        Assert.DoesNotContain("made-up-token", model.DiscordWebhookError, StringComparison.Ordinal);
        Assert.Empty(_credentials.Writes);
    }

    [Fact]
    public void DiscordSavesWithNoStorageFieldFilledIn()
    {
        SettingsViewModel model = Open();
        model.Provider = "discord";
        model.DiscordWebhook = Webhook;

        model.Save();

        Assert.True(model.Saved);
        Assert.Null(model.EndpointError);
        Assert.Null(model.CredentialError);
    }

    [Fact]
    public void SavingDiscordKeepsTheStorageValuesOfTheOtherProviders()
    {
        _file.Save(S3SetUp());
        SettingsViewModel model = Open();
        model.Provider = "discord";
        model.DiscordWebhook = Webhook;

        model.Save();

        StorageSettings storage = _file.Load().Storage;
        Assert.Equal("discord", storage.Provider);
        Assert.Equal("https://storage.example.test", storage.Endpoint);
        Assert.Equal("made-up-bucket", storage.Bucket);
        Assert.Equal("stored-name", storage.CredentialName);
    }

    [Fact]
    public void SwitchingBackToR2RestoresTheStorageFieldValues()
    {
        _file.Save(S3SetUp());
        _credentials.Stored["stored-name"] = new StoredCredential("made-up-key-id", "made-up-secret-value");
        SettingsViewModel model = Open();

        model.Provider = "discord";
        model.Provider = "r2";

        Assert.True(model.ShowsStorageFields);
        Assert.Equal("https://storage.example.test", model.Endpoint);
        Assert.Equal("made-up-bucket", model.Bucket);
        Assert.Equal("https://share.example.test", model.PublicBaseUrl);
        Assert.Equal("made-up-key-id", model.AccessKeyId);
    }

    [Fact]
    public void SavingBackOnR2UsesTheStorageFieldsAsBefore()
    {
        _file.Save(S3SetUp() with { Storage = S3SetUp().Storage with { Provider = "discord" } });
        _credentials.Stored["stored-name"] = new StoredCredential("made-up-key-id", "made-up-secret-value");
        SettingsViewModel model = Open();

        model.Provider = "r2";
        model.Save();

        Assert.True(model.Saved);
        Assert.Equal("r2", _file.Load().Storage.Provider);
        Assert.Empty(_credentials.Writes);
    }

    [Fact]
    public void TheStatusLineReadsLinksGoToDiscordWhenSetUp()
    {
        _credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", Stored);
        SettingsViewModel model = Open();

        model.Provider = "discord";

        Assert.Equal("Links go to Discord", model.SharingStatus);
    }

    [Fact]
    public void TheStatusLineFollowsATypedWebhook()
    {
        SettingsViewModel model = Open();
        model.Provider = "discord";
        Assert.Equal("Not set up: captures stay on this PC", model.SharingStatus);

        model.DiscordWebhook = Webhook;

        Assert.Equal("Links go to Discord", model.SharingStatus);
    }

    [Fact]
    public void TheCredentialNameInTheSettingsIsTheOneUsed()
    {
        _file.Save(new ShareSettings { Discord = new DiscordSettings { CredentialName = "my-discord" } });
        SettingsViewModel model = Open();
        model.Provider = "discord";
        model.DiscordWebhook = Webhook;

        model.Save();

        Assert.Equal("my-discord", Assert.Single(_credentials.Writes).Name);
    }

    [Fact]
    public void SavingKeepsTheTimeoutAndTheSizeLimitTheUserSetInTheFile()
    {
        _file.Save(new ShareSettings { Discord = new DiscordSettings { TimeoutSeconds = 25, MaxUploadMegabytes = 8 } });
        SettingsViewModel model = Open();
        model.Provider = "discord";
        model.DiscordWebhook = Webhook;

        model.Save();

        Assert.Equal(25, _file.Load().Discord.TimeoutSeconds);
        Assert.Equal(8, _file.Load().Discord.MaxUploadMegabytes);
    }
}
