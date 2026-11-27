using System.Globalization;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Settings;

/// <summary>
/// The settings window: the sharing host, the default expiry, the hotkeys and the recording limits. Save checks every field,
/// writes settings.json and the credential, and applies the result to the running app; Cancel writes nothing.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    /// <summary>Known-good stand-ins so a check can go on to the next field after one failed.</summary>
    private const string ValidAddress = "https://valid.example";

    private const string ValidText = "valid";

    private static readonly BarAction[] HotkeyActions = [BarAction.Region, BarAction.FullScreen, BarAction.Window, BarAction.Record];

    private readonly SettingsFile _file;
    private readonly LiveSettings _live;
    private readonly ICredentialStore _credentials;
    private readonly HotkeyBinder _binder;
    private string _provider;
    private string _endpoint;
    private string _bucket;
    private string _region;
    private string _publicBaseUrl;
    private string _accessKeyId;
    private string _secretAccessKey = string.Empty;
    private ExpiryChoice _selectedExpiry;
    private string _maxSizeMegabytes;
    private string _frameRate;
    private string? _endpointError;
    private string? _bucketError;
    private string? _regionError;
    private string? _publicBaseUrlError;
    private string? _credentialError;
    private string? _maxSizeError;
    private string? _frameRateError;
    private string? _saveError;

    public SettingsViewModel(SettingsFile file, LiveSettings live, ICredentialStore credentials, HotkeyBinder binder, IReadOnlyList<string> providers)
    {
        _file = file;
        _live = live;
        _credentials = credentials;
        _binder = binder;
        Providers = providers;
        ShareSettings current = file.Load();
        StorageSettings storage = current.Storage;
        _provider = storage.Provider;
        _endpoint = storage.Endpoint;
        _bucket = storage.Bucket;
        _region = storage.Region;
        _publicBaseUrl = storage.PublicBaseUrl;
        StoredCredential? stored = credentials.Read(CredentialNameFor(storage));
        _accessKeyId = stored?.AccessKeyId ?? string.Empty;
        SecretPlaceholder = stored is null ? string.Empty : Strings.Get("settings.secret.saved");
        ExpiryChoices = ChoicesFor(storage, current.Expiry);
        _selectedExpiry = ExpiryChoices.First(choice => choice.Days == current.Expiry.DefaultDays);
        Hotkeys = [.. HotkeyActions.Select(action => new HotkeyRow(action, current.Hotkeys.Chords().GetValueOrDefault(action, string.Empty)))];
        _maxSizeMegabytes = current.Recording.MaxSizeMegabytes.ToString(CultureInfo.InvariantCulture);
        _frameRate = current.Recording.FrameRate.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Raised after a save or a cancel; the window closes.</summary>
    public event EventHandler? CloseRequested;

    public IReadOnlyList<string> Providers { get; }

    public IReadOnlyList<ExpiryChoice> ExpiryChoices { get; }

    public IReadOnlyList<HotkeyRow> Hotkeys { get; }

    /// <summary>Shown in the empty secret box when a credential is stored; the secret itself is never loaded.</summary>
    public string SecretPlaceholder { get; }

    /// <summary>True after a save that passed every check.</summary>
    public bool Saved { get; private set; }

    public string Provider
    {
        get => _provider;
        set => Set(ref _provider, value);
    }

    public string Endpoint
    {
        get => _endpoint;
        set
        {
            Set(ref _endpoint, value);
            OnChanged(nameof(SharingStatus));
        }
    }

    public string Bucket
    {
        get => _bucket;
        set
        {
            Set(ref _bucket, value);
            OnChanged(nameof(SharingStatus));
        }
    }

    public string Region
    {
        get => _region;
        set => Set(ref _region, value);
    }

    public string PublicBaseUrl
    {
        get => _publicBaseUrl;
        set
        {
            Set(ref _publicBaseUrl, value);
            OnChanged(nameof(SharingStatus));
        }
    }

    public string AccessKeyId
    {
        get => _accessKeyId;
        set => Set(ref _accessKeyId, value);
    }

    /// <summary>What the user typed this time. It goes to the credential store and nowhere else.</summary>
    public string SecretAccessKey
    {
        get => _secretAccessKey;
        set => Set(ref _secretAccessKey, value);
    }

    public ExpiryChoice SelectedExpiry
    {
        get => _selectedExpiry;
        set => Set(ref _selectedExpiry, value);
    }

    public string MaxSizeMegabytes
    {
        get => _maxSizeMegabytes;
        set => Set(ref _maxSizeMegabytes, value);
    }

    public string FrameRate
    {
        get => _frameRate;
        set => Set(ref _frameRate, value);
    }

    /// <summary>Whether links will be made, from what is typed now.</summary>
    public string SharingStatus
    {
        get
        {
            if (!IsSharingFilledIn)
            {
                return Strings.Get("settings.sharing.off");
            }

            return Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out Uri? link)
                ? Strings.Get("settings.sharing.on", link.Host)
                : Strings.Get("settings.sharing.partial");
        }
    }

    public string? EndpointError
    {
        get => _endpointError;
        private set => Set(ref _endpointError, value);
    }

    public string? BucketError
    {
        get => _bucketError;
        private set => Set(ref _bucketError, value);
    }

    public string? RegionError
    {
        get => _regionError;
        private set => Set(ref _regionError, value);
    }

    public string? PublicBaseUrlError
    {
        get => _publicBaseUrlError;
        private set => Set(ref _publicBaseUrlError, value);
    }

    public string? CredentialError
    {
        get => _credentialError;
        private set => Set(ref _credentialError, value);
    }

    public string? MaxSizeError
    {
        get => _maxSizeError;
        private set => Set(ref _maxSizeError, value);
    }

    public string? FrameRateError
    {
        get => _frameRateError;
        private set => Set(ref _frameRateError, value);
    }

    /// <summary>A failure that belongs to no one field, such as the credential store refusing the write.</summary>
    public string? SaveError
    {
        get => _saveError;
        private set => Set(ref _saveError, value);
    }

    private bool IsSharingFilledIn =>
        new[] { Endpoint, Bucket, PublicBaseUrl, AccessKeyId, SecretAccessKey }.Any(text => !string.IsNullOrWhiteSpace(text));

    /// <summary>Checks every field. With a mistake it shows the errors and stays open; otherwise it saves, applies and closes.</summary>
    public void Save()
    {
        ClearErrors();
        ShareSettings current = _file.Load();
        (StorageSettings storage, CredentialPlan credential) = PlanStorage(current.Storage);
        RecordingSettings recording = PlanRecording(current.Recording);
        HotkeySettings hotkeys = PlanHotkeys(current.Hotkeys);
        if (HasErrors)
        {
            return;
        }

        ShareSettings next = current with
        {
            Storage = storage,
            Expiry = current.Expiry with { DefaultDays = SelectedExpiry.Days },
            Recording = recording,
            Hotkeys = hotkeys
        };
        try
        {
            if (credential.Write is { } key)
            {
                _credentials.Write(storage.CredentialName, key);
            }

            _file.Save(next);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            SaveError = Strings.Get("settings.save.failed", error.Message);
            return;
        }

        _live.Replace(next);
        _binder.Apply(next.Hotkeys);
        Saved = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Closes the window and writes nothing.</summary>
    public void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private bool HasErrors =>
        new[] { EndpointError, BucketError, RegionError, PublicBaseUrlError, CredentialError, MaxSizeError, FrameRateError }.Any(error => error is not null)
        || Hotkeys.Any(row => row.Error is not null);

    private void ClearErrors()
    {
        EndpointError = BucketError = RegionError = PublicBaseUrlError = CredentialError = MaxSizeError = FrameRateError = SaveError = null;
        foreach (HotkeyRow row in Hotkeys)
        {
            row.Error = null;
        }
    }

    private static string CredentialNameFor(StorageSettings storage) =>
        string.IsNullOrWhiteSpace(storage.CredentialName) ? ShareDefaults.CredentialName : storage.CredentialName;

    private static IReadOnlyList<ExpiryChoice> ChoicesFor(StorageSettings storage, ExpirySettings expiry)
    {
        IEnumerable<int> days = storage.ExpiryBucketDays;
        if (expiry.DefaultDays is { } chosen)
        {
            days = days.Append(chosen);
        }

        return [new ExpiryChoice(null), .. days.Distinct().Order().Select(count => new ExpiryChoice(count))];
    }

    /// <summary>The credential to write; null when the stored one stays as it is.</summary>
    private sealed record CredentialPlan(StoredCredential? Write);

    private (StorageSettings Storage, CredentialPlan Credential) PlanStorage(StorageSettings current)
    {
        if (!IsSharingFilledIn)
        {
            return (current with { Provider = Provider, Endpoint = string.Empty, Bucket = string.Empty, PublicBaseUrl = string.Empty, CredentialName = string.Empty }, new CredentialPlan(null));
        }

        StorageSettings storage = current with
        {
            Provider = Provider,
            Endpoint = Endpoint.Trim(),
            Bucket = Bucket.Trim(),
            Region = Region.Trim(),
            PublicBaseUrl = PublicBaseUrl.Trim(),
            CredentialName = CredentialNameFor(current)
        };
        ShowStorageErrors(storage);
        return (storage, PlanCredential(storage.CredentialName));
    }

    /// <summary>Validate stops at the first bad setting, so each failure is shown, then replaced by a good value to reach the next.</summary>
    private void ShowStorageErrors(StorageSettings storage)
    {
        StorageSettings checking = storage;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                checking.Validate();
                return;
            }
            catch (InvalidStorageSettingsException error)
            {
                switch (error.Setting)
                {
                    case "Storage.Endpoint":
                        EndpointError = error.Message;
                        checking = checking with { Endpoint = ValidAddress };
                        break;
                    case "Storage.Bucket":
                        BucketError = error.Message;
                        checking = checking with { Bucket = ValidText };
                        break;
                    case "Storage.Region":
                        RegionError = error.Message;
                        checking = checking with { Region = ValidText };
                        break;
                    case "Storage.PublicBaseUrl":
                        PublicBaseUrlError = error.Message;
                        checking = checking with { PublicBaseUrl = ValidAddress };
                        break;
                    default:
                        SaveError = error.Message;
                        return;
                }
            }
        }
    }

    private CredentialPlan PlanCredential(string name)
    {
        StoredCredential? stored = _credentials.Read(name);
        if (string.IsNullOrWhiteSpace(AccessKeyId))
        {
            CredentialError = Strings.Get("error.credential.needed");
            return new CredentialPlan(null);
        }

        if (!string.IsNullOrEmpty(SecretAccessKey))
        {
            return new CredentialPlan(new StoredCredential(AccessKeyId.Trim(), SecretAccessKey));
        }

        if (stored is null)
        {
            CredentialError = Strings.Get("error.credential.needed");
        }
        else if (stored.AccessKeyId != AccessKeyId.Trim())
        {
            CredentialError = Strings.Get("error.credential.secret");
        }

        return new CredentialPlan(null);
    }

    private RecordingSettings PlanRecording(RecordingSettings current)
    {
        bool sizeOk = double.TryParse(MaxSizeMegabytes, NumberStyles.Float, CultureInfo.InvariantCulture, out double size) && double.IsFinite(size) && size > 0;
        bool rateOk = int.TryParse(FrameRate, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rate) && rate > 0;
        MaxSizeError = sizeOk ? null : Strings.Get("error.setting.positive", Strings.Get("settings.recording.size"));
        FrameRateError = rateOk ? null : Strings.Get("error.setting.positive", Strings.Get("settings.recording.fps"));
        return sizeOk && rateOk ? current with { MaxSizeMegabytes = size, FrameRate = rate } : current;
    }

    private HotkeySettings PlanHotkeys(HotkeySettings current)
    {
        Dictionary<BarAction, string> chosen = [];
        foreach (HotkeyRow row in Hotkeys)
        {
            if (string.IsNullOrWhiteSpace(row.Chord))
            {
                chosen[row.Action] = string.Empty;
            }
            else if (HotkeyChord.TryParse(row.Chord, out HotkeyChord? chord, out string? error))
            {
                chosen[row.Action] = chord.ToString();
            }
            else
            {
                row.Error = error;
            }
        }

        return current with
        {
            Region = chosen.GetValueOrDefault(BarAction.Region, current.Region),
            FullScreen = chosen.GetValueOrDefault(BarAction.FullScreen, current.FullScreen),
            Window = chosen.GetValueOrDefault(BarAction.Window, current.Window),
            Record = chosen.GetValueOrDefault(BarAction.Record, current.Record)
        };
    }
}
