using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
    private const string KeyId = "made-up-key-id";
    private const string Secret = "made-up-secret-value";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-settings-test-{Guid.NewGuid():N}");
    private readonly FakeCredentialStore _credentials = new();
    private readonly FakeGlobalHotkeys _hotkeys = new();
    private readonly SettingsFile _file;
    private readonly LiveSettings _live;
    private readonly HotkeyBinder _binder;
    private SettingsViewModel? _model;

    public SettingsViewModelTests()
    {
        _file = new SettingsFile(SettingsFile.DefaultPath(_folder));
        _live = new LiveSettings(_file.Load());
        BarViewModel bar = new(
            new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
            new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()));
        _binder = new HotkeyBinder(_hotkeys, bar, _live.Current.Hotkeys);
        _binder.Start();
    }

    private SettingsViewModel Open(ShareSettings? current = null)
    {
        if (current is not null)
        {
            _file.Save(current);
            _live.Replace(current);
        }

        _model = new SettingsViewModel(_file, _live, _credentials, _binder, ["r2", "s3"]);
        return _model;
    }

    private static ShareSettings SetUp(string credentialName = "stored-name") => new()
    {
        Storage = new StorageSettings
        {
            Provider = "s3",
            Endpoint = "https://storage.example.test",
            Bucket = "made-up-bucket",
            PublicBaseUrl = "https://share.example.test",
            CredentialName = credentialName
        },
        Expiry = new ExpirySettings { DefaultDays = 7 }
    };

    private static void FillValid(SettingsViewModel model)
    {
        model.Endpoint = "https://storage.example.test";
        model.Bucket = "made-up-bucket";
        model.PublicBaseUrl = "https://share.example.test";
        model.AccessKeyId = KeyId;
        model.SecretAccessKey = Secret;
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void ItLoadsTheCurrentValuesAndNeverTheStoredSecret()
    {
        _credentials.Stored["stored-name"] = new StoredCredential(KeyId, Secret);

        SettingsViewModel model = Open(SetUp() with { Recording = new RecordingSettings { MaxSizeMegabytes = 25, FrameRate = 24 } });

        Assert.Equal("s3", model.Provider);
        Assert.Equal("https://storage.example.test", model.Endpoint);
        Assert.Equal("made-up-bucket", model.Bucket);
        Assert.Equal("auto", model.Region);
        Assert.Equal(KeyId, model.AccessKeyId);
        Assert.Equal(string.Empty, model.SecretAccessKey);
        Assert.Equal("Saved", model.SecretPlaceholder);
        Assert.Equal(7, model.SelectedExpiry.Days);
        Assert.Equal("Ctrl+PrintScreen", model.Hotkeys.Single(row => row.Action == BarAction.Region).Chord);
        Assert.Equal("25", model.MaxSizeMegabytes);
        Assert.Equal("24", model.FrameRate);
        Assert.Equal("Links go to share.example.test", model.SharingStatus);
    }

    [Fact]
    public void WithNothingSetUpTheStatusSaysCapturesStayOnThisPc()
    {
        SettingsViewModel model = Open();

        Assert.Equal("Not set up: captures stay on this PC", model.SharingStatus);
        Assert.Equal(string.Empty, model.SecretPlaceholder);
    }

    [Fact]
    public void TheExpiryChoicesAreNeverThenTheBucketDays()
    {
        SettingsViewModel model = Open();

        Assert.Equal([null, 1, 7, 30, 90], model.ExpiryChoices.Select(choice => choice.Days));
        Assert.Equal("Never", model.ExpiryChoices[0].Label);
        Assert.Equal("1 day", model.ExpiryChoices[1].Label);
        Assert.Equal("7 days", model.ExpiryChoices[2].Label);
    }

    [Fact]
    public void AnInvalidEndpointKeepsTheWindowOpenWithTheErrorOnThatField()
    {
        SettingsViewModel model = Open();
        FillValid(model);
        model.Endpoint = "not an address";
        bool closed = false;
        model.CloseRequested += (_, _) => closed = true;

        model.Save();

        Assert.False(closed);
        Assert.False(model.Saved);
        Assert.Equal("Enter a full https:// address.", model.EndpointError);
        Assert.Null(model.BucketError);
        Assert.False(File.Exists(SettingsFile.DefaultPath(_folder)));
        Assert.Empty(_credentials.Writes);
    }

    [Fact]
    public void EveryInvalidFieldShowsItsOwnError()
    {
        SettingsViewModel model = Open();
        FillValid(model);
        model.Endpoint = "not an address";
        model.Bucket = "";
        model.PublicBaseUrl = "ftp://share.example.test";

        model.Save();

        Assert.Equal("Enter a full https:// address.", model.EndpointError);
        Assert.Equal("Enter the bucket name.", model.BucketError);
        Assert.Equal("Enter a full https:// address.", model.PublicBaseUrlError);
    }

    [Fact]
    public void FieldErrorsAreWindowTextNotTheSettingsJsonMessage()
    {
        SettingsViewModel model = Open();
        FillValid(model);
        model.Endpoint = "not an address";
        model.Bucket = "";
        model.Region = "";
        model.PublicBaseUrl = "";

        model.Save();

        Assert.Equal("Enter a full https:// address.", model.EndpointError);
        Assert.Equal("Enter the bucket name.", model.BucketError);
        Assert.Equal("Enter the region.", model.RegionError);
        Assert.Equal("Enter the public link base URL.", model.PublicBaseUrlError);
        Assert.DoesNotContain("settings.json", string.Concat(model.EndpointError, model.BucketError, model.RegionError, model.PublicBaseUrlError));
    }

    [Fact]
    public void EveryWindowErrorEndsWithAFullStop()
    {
        SettingsViewModel model = Open();
        model.Bucket = "made-up-bucket";
        model.Endpoint = "x";
        model.MaxSizeMegabytes = "abc";
        model.FrameRate = "0";

        model.Save();

        Assert.EndsWith(".", model.MaxSizeError);
        Assert.EndsWith(".", model.FrameRateError);
        Assert.EndsWith(".", model.CredentialError);
        Assert.EndsWith(".", model.EndpointError);
    }

    [Fact]
    public void TheProviderIsShownByItsNameAndStoredByItsValue()
    {
        SettingsViewModel model = Open();

        Assert.Equal(["Cloudflare R2", "S3-compatible"], model.Providers.Select(provider => provider.Label));
        Assert.Equal(["r2", "s3"], model.Providers.Select(provider => provider.Value));
        model.Provider = "s3";
        FillValid(model);
        model.Save();
        Assert.Equal("s3", _file.Load().Storage.Provider);
    }

    [Fact]
    public void AValidSaveWritesTheFileAndTheCredentialAndAppliesItLive()
    {
        SettingsViewModel model = Open();
        FillValid(model);
        model.SelectedExpiry = model.ExpiryChoices.Single(choice => choice.Days == 90);
        model.MaxSizeMegabytes = "20";
        model.FrameRate = "15";
        bool closed = false;
        model.CloseRequested += (_, _) => closed = true;

        model.Save();

        Assert.True(closed);
        Assert.True(model.Saved);
        ShareSettings saved = _file.Load();
        Assert.Equal("https://storage.example.test", saved.Storage.Endpoint);
        Assert.Equal("nomercy-share", saved.Storage.CredentialName);
        Assert.Equal(90, saved.Expiry.DefaultDays);
        Assert.Equal(20, saved.Recording.MaxSizeMegabytes);
        Assert.Equal(15, saved.Recording.FrameRate);
        (string name, StoredCredential credential) = Assert.Single(_credentials.Writes);
        Assert.Equal("nomercy-share", name);
        Assert.Equal(new StoredCredential(KeyId, Secret), credential);
        Assert.DoesNotContain(Secret, File.ReadAllText(SettingsFile.DefaultPath(_folder)));
        Assert.Equal("https://storage.example.test", _live.Current.Storage.Endpoint);
        Assert.Equal(90, _live.Current.Expiry.DefaultDays);
    }

    [Fact]
    public void AStoredCredentialNameIsKeptAndNeverRenamed()
    {
        SettingsViewModel model = Open(SetUp("stored-name"));
        model.AccessKeyId = KeyId;
        model.SecretAccessKey = Secret;

        model.Save();

        Assert.Equal("stored-name", Assert.Single(_credentials.Writes).Name);
        Assert.Equal("stored-name", _file.Load().Storage.CredentialName);
    }

    [Fact]
    public void AnEmptySecretKeepsTheStoredOneAndWritesNoCredential()
    {
        _credentials.Stored["stored-name"] = new StoredCredential(KeyId, Secret);
        SettingsViewModel model = Open(SetUp());
        model.Bucket = "another-bucket";

        model.Save();

        Assert.True(model.Saved);
        Assert.Empty(_credentials.Writes);
        Assert.Equal("another-bucket", _file.Load().Storage.Bucket);
    }

    [Fact]
    public void ANewKeyIdWithoutASecretAsksForTheSecret()
    {
        _credentials.Stored["stored-name"] = new StoredCredential(KeyId, Secret);
        SettingsViewModel model = Open(SetUp());
        model.AccessKeyId = "another-key-id";

        model.Save();

        Assert.False(model.Saved);
        Assert.NotNull(model.CredentialError);
        Assert.Empty(_credentials.Writes);
    }

    [Fact]
    public void SharingSetUpWithNoCredentialAtAllAsksForBoth()
    {
        SettingsViewModel model = Open();
        FillValid(model);
        model.AccessKeyId = "";
        model.SecretAccessKey = "";

        model.Save();

        Assert.False(model.Saved);
        Assert.NotNull(model.CredentialError);
    }

    [Fact]
    public void ClearingEverythingTurnsSharingOffWithoutValidation()
    {
        _credentials.Stored["stored-name"] = new StoredCredential(KeyId, Secret);
        SettingsViewModel model = Open(SetUp());
        model.Endpoint = "";
        model.Bucket = "";
        model.PublicBaseUrl = "";
        model.AccessKeyId = "";

        model.Save();

        Assert.True(model.Saved);
        Assert.False(_file.Load().Storage.IsSetUp);
        Assert.Empty(_credentials.Writes);
    }

    [Fact]
    public void HotkeysAreRegisteredAgainOnSave()
    {
        SettingsViewModel model = Open();
        FillValid(model);
        model.Hotkeys.Single(row => row.Action == BarAction.Region).Chord = "Ctrl+Shift+F9";
        model.Hotkeys.Single(row => row.Action == BarAction.Record).Chord = "";

        model.Save();

        Assert.Equal(2, _hotkeys.RegisterCalls);
        Assert.Equal(HotkeyChord.Parse("Ctrl+Shift+F9"), _hotkeys.Registered[BarAction.Region]);
        Assert.False(_hotkeys.Registered.ContainsKey(BarAction.Record));
        Assert.Equal("", _file.Load().Hotkeys.Record);
        Assert.Equal(1, _hotkeys.PressedSubscribers);
    }

    [Fact]
    public void AnInvalidChordShowsItsErrorOnItsRowAndSavesNothing()
    {
        SettingsViewModel model = Open();
        FillValid(model);
        HotkeyRow row = model.Hotkeys.Single(row => row.Action == BarAction.Window);
        row.Chord = "Ctrl+Nonsense";

        model.Save();

        Assert.False(model.Saved);
        Assert.NotNull(row.Error);
        Assert.Equal(1, _hotkeys.RegisterCalls);
        Assert.Empty(_credentials.Writes);
    }

    [Fact]
    public void ClearingARowTurnsItsHotkeyOff()
    {
        SettingsViewModel model = Open();
        HotkeyRow row = model.Hotkeys.Single(row => row.Action == BarAction.FullScreen);

        row.Clear();

        Assert.Equal(string.Empty, row.Chord);
    }

    [Theory]
    [InlineData("abc", "30")]
    [InlineData("0", "30")]
    [InlineData("50", "0")]
    [InlineData("50", "x")]
    public void ARecordingLimitThatIsNotAPositiveNumberIsRefused(string size, string rate)
    {
        SettingsViewModel model = Open();
        model.MaxSizeMegabytes = size;
        model.FrameRate = rate;

        model.Save();

        Assert.False(model.Saved);
        Assert.True(model.MaxSizeError is not null || model.FrameRateError is not null);
    }

    [Fact]
    public void ACredentialStoreThatFailsLeavesTheFileAndTheLiveSettingsAlone()
    {
        _credentials.WriteThrows = new InvalidOperationException("store is locked");
        SettingsViewModel model = Open();
        FillValid(model);

        model.Save();

        Assert.False(model.Saved);
        Assert.NotNull(model.SaveError);
        Assert.False(File.Exists(SettingsFile.DefaultPath(_folder)));
        Assert.Equal(string.Empty, _live.Current.Storage.Endpoint);
        Assert.DoesNotContain(Secret, model.SaveError);
    }

    [Fact]
    public void CancelWritesNothingAndClosesTheWindow()
    {
        SettingsViewModel model = Open();
        FillValid(model);
        bool closed = false;
        model.CloseRequested += (_, _) => closed = true;

        model.Cancel();

        Assert.True(closed);
        Assert.False(model.Saved);
        Assert.False(File.Exists(SettingsFile.DefaultPath(_folder)));
        Assert.Empty(_credentials.Writes);
        Assert.Equal(1, _hotkeys.RegisterCalls);
    }

    [Fact]
    public void ASaveKeepsWhatOtherPartsWroteToTheFileMeanwhile()
    {
        SettingsViewModel model = Open();
        FillValid(model);
        new BarPositionStore(_file).Save(new BarPosition(10, 20));

        model.Save();

        Assert.Equal(new BarPosition(10, 20), _file.Load().Bar.Position);
    }
}
