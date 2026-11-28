using System.Globalization;
using System.Windows.Input;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Discord;
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

    private static readonly BarAction[] HotkeyActions = [BarAction.Show, BarAction.Region, BarAction.FullScreen, BarAction.Window, BarAction.Record];

    private readonly SettingsFile _file;
    private readonly LiveSettings _live;
    private readonly ICredentialStore _credentials;
    private readonly HotkeyBinder _binder;
    private readonly bool _discordStored;
    private IDisposable? _recording;
    private string _provider;
    private string _endpoint;
    private string _bucket;
    private string _region;
    private string _publicBaseUrl;
    private string _accessKeyId;
    private string _secretAccessKey = string.Empty;
    private ExpiryChoice _selectedExpiry;
    private string _discordWebhook = string.Empty;
    private string? _discordWebhookError;
    private string _maxSizeMegabytes;
    private string _frameRate;
    private bool _recordSystemSound;
    private bool _recordMicrophone;
    private AudioSettings _followedAudio;
    private bool _showBarInCaptures;
    private readonly IEncoderProbe? _probe;
    private ResolutionChoice _selectedResolution;
    private OptionChoice _selectedCodec;
    private OptionChoice _selectedSpeed;
    private OptionChoice _selectedEncoder;
    private IReadOnlyList<OptionChoice> _encoderChoices;
    private string _wantedEncoder;
    private string? _encoderNote;
    private string? _endpointError;
    private string? _bucketError;
    private string? _regionError;
    private string? _publicBaseUrlError;
    private string? _credentialError;
    private string? _maxSizeError;
    private string? _frameRateError;
    private string? _saveError;
    private SettingsSection _selectedSection;

    public SettingsViewModel(SettingsFile file, LiveSettings live, ICredentialStore credentials, HotkeyBinder binder, IReadOnlyList<string> providers, IEncoderProbe? encoders = null)
    {
        _probe = encoders;
        _file = file;
        _live = live;
        _credentials = credentials;
        _binder = binder;
        Providers = [.. providers.Select(value => new ProviderChoice(value))];
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
        _discordStored = credentials.Read(DiscordNameFor(current.Discord)) is not null;
        DiscordWebhookPlaceholder = _discordStored ? Strings.Get("settings.secret.saved") : string.Empty;
        Hotkeys = [.. HotkeyActions.Select(action => new HotkeyRow(action, current.Hotkeys.Chords().GetValueOrDefault(action, string.Empty)))];
        _maxSizeMegabytes = current.Recording.MaxSizeMegabytes.ToString(CultureInfo.InvariantCulture);
        _frameRate = current.Recording.FrameRate.ToString(CultureInfo.InvariantCulture);
        _recordSystemSound = current.Recording.Audio.SystemSound;
        _recordMicrophone = current.Recording.Audio.Microphone;
        _followedAudio = live.Current.Recording.Audio;
        _showBarInCaptures = current.Bar.ShowInCaptures;
        live.Changed += OnLiveChanged;

        RecordingSettings recording = current.Recording;
        ResolutionChoice savedSize = recording.MaxWidth <= 0 || recording.MaxHeight <= 0 ? new(0, 0) : new(recording.MaxWidth, recording.MaxHeight);
        IReadOnlyList<ResolutionChoice> sizes = [.. ShareDefaults.RecordingSizeChoices.Select(size => new ResolutionChoice(size.Width, size.Height))];
        ResolutionChoices = sizes.Contains(savedSize) ? sizes : [.. sizes, savedSize]; // a size set by hand stays a choice, so saving never drops it
        _selectedResolution = savedSize;
        VideoEncoder encoder = VideoEncoder.From(recording);
        CodecChoices = [.. VideoEncoder.Codecs.Select(value => new OptionChoice(CodecGroup, value))];
        SpeedChoices = [.. VideoEncoder.Speeds.Select(value => new OptionChoice(SpeedGroup, value))];
        _selectedCodec = CodecChoices.First(choice => choice.Value == encoder.Codec);
        _selectedSpeed = SpeedChoices.First(choice => choice.Value == encoder.Speed);
        _wantedEncoder = encoder.Family;
        _encoderChoices = [.. VideoEncoder.Families.Where(family => family == VideoEncoder.Cpu || (_probe is null && family == _wantedEncoder)).Select(EncoderChoice)];
        _selectedEncoder = _encoderChoices.First(choice => choice.Value == _wantedEncoder || choice.Value == VideoEncoder.Cpu);
        EncodersChecked = CheckEncodersAsync();
    }

    private const string CodecGroup = "settings.recording.codec";

    private const string SpeedGroup = "settings.recording.speed";

    private const string EncoderGroup = "settings.recording.encoder";

    private static OptionChoice EncoderChoice(string family) => new(EncoderGroup, family);

    /// <summary>The encoders that passed the test encode for the picked codec replace the list; the saved one stays picked when it passed.</summary>
    private async Task CheckEncodersAsync()
    {
        if (_probe is null)
        {
            return;
        }

        string codec = SelectedCodec.Value;
        IReadOnlyList<string> working;
        try
        {
            working = await _probe.WorkingFamiliesAsync(codec);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            working = [VideoEncoder.Cpu]; // no ffmpeg to test with: the CPU is what a recording would try
        }

        if (codec != SelectedCodec.Value)
        {
            return; // a newer codec pick runs its own check
        }

        EncoderChoices = [.. VideoEncoder.Families.Where(family => family == VideoEncoder.Cpu || working.Contains(family)).Select(EncoderChoice)];
        OptionChoice? wanted = EncoderChoices.FirstOrDefault(choice => choice.Value == _wantedEncoder);
        _selectedEncoder = wanted ?? EncoderChoices[0];
        OnChanged(nameof(SelectedEncoder));
        EncoderNote = wanted is null ? Strings.Get("settings.recording.encoder.missing", EncoderChoice(_wantedEncoder).Label) : null;
    }

    /// <summary>Raised after a save or a cancel; the window closes.</summary>
    public event EventHandler? CloseRequested;

    public IReadOnlyList<ProviderChoice> Providers { get; }

    public IReadOnlyList<ExpiryChoice> ExpiryChoices { get; }

    public IReadOnlyList<HotkeyRow> Hotkeys { get; }

    /// <summary>The section the window shows; it opens on General.</summary>
    public SettingsSection SelectedSection
    {
        get => _selectedSection;
        set
        {
            Set(ref _selectedSection, value);
            OnChanged(nameof(SelectedSectionIndex));
            OnChanged(nameof(ShowsGeneral));
            OnChanged(nameof(ShowsSharing));
            OnChanged(nameof(ShowsLinks));
            OnChanged(nameof(ShowsHotkeys));
            OnChanged(nameof(ShowsRecording));
        }
    }

    /// <summary>The same choice as a list position, for the section list.</summary>
    public int SelectedSectionIndex
    {
        get => (int)_selectedSection;
        set
        {
            if (Enum.IsDefined(typeof(SettingsSection), value))
            {
                SelectedSection = (SettingsSection)value;
            }
        }
    }

    public bool ShowsGeneral => SelectedSection == SettingsSection.General;

    public bool ShowsSharing => SelectedSection == SettingsSection.Sharing;

    public bool ShowsLinks => SelectedSection == SettingsSection.Links;

    public bool ShowsHotkeys => SelectedSection == SettingsSection.Hotkeys;

    public bool ShowsRecording => SelectedSection == SettingsSection.Recording;

    /// <summary>Shown in the empty secret box when a credential is stored; the secret itself is never loaded.</summary>
    public string SecretPlaceholder { get; }

    /// <summary>True after a save that passed every check.</summary>
    public bool Saved { get; private set; }

    public string Provider
    {
        get => _provider;
        set
        {
            Set(ref _provider, value);
            OnChanged(nameof(IsDiscord));
            OnChanged(nameof(ShowsStorageFields));
            OnChanged(nameof(SharingStatus));
        }
    }

    /// <summary>True while Discord is the picked provider: the window then asks for the webhook address only.</summary>
    public bool IsDiscord => string.Equals(Provider, DiscordSettings.ProviderName, StringComparison.OrdinalIgnoreCase);

    /// <summary>True while the S3-style fields (endpoint, bucket, region, link base URL, key) are shown.</summary>
    public bool ShowsStorageFields => !IsDiscord;

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

    /// <summary>What the user typed this time. It goes to the credential store and nowhere else.</summary>
    public string DiscordWebhook
    {
        get => _discordWebhook;
        set
        {
            Set(ref _discordWebhook, value);
            OnChanged(nameof(SharingStatus));
        }
    }

    /// <summary>Shown in the empty webhook box when an address is stored; the address itself is never loaded.</summary>
    public string DiscordWebhookPlaceholder { get; }

    public string? DiscordWebhookError
    {
        get => _discordWebhookError;
        private set => Set(ref _discordWebhookError, value);
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

    public bool RecordSystemSound
    {
        get => _recordSystemSound;
        set => Set(ref _recordSystemSound, value);
    }

    public bool RecordMicrophone
    {
        get => _recordMicrophone;
        set => Set(ref _recordMicrophone, value);
    }

    public bool ShowBarInCaptures
    {
        get => _showBarInCaptures;
        set => Set(ref _showBarInCaptures, value);
    }

    public IReadOnlyList<ResolutionChoice> ResolutionChoices { get; }

    public ResolutionChoice SelectedResolution
    {
        get => _selectedResolution;
        set => Set(ref _selectedResolution, value);
    }

    public IReadOnlyList<OptionChoice> CodecChoices { get; }

    /// <summary>A new codec runs the encoder test again, since a GPU can do one codec and not another.</summary>
    public OptionChoice SelectedCodec
    {
        get => _selectedCodec;
        set
        {
            if (value is null || value == _selectedCodec)
            {
                return;
            }

            Set(ref _selectedCodec, value);
            EncodersChecked = CheckEncodersAsync();
        }
    }

    public IReadOnlyList<OptionChoice> SpeedChoices { get; }

    public OptionChoice SelectedSpeed
    {
        get => _selectedSpeed;
        set => Set(ref _selectedSpeed, value);
    }

    /// <summary>The CPU, plus each GPU encoder that passed a test encode on this PC for the picked codec.</summary>
    public IReadOnlyList<OptionChoice> EncoderChoices
    {
        get => _encoderChoices;
        private set => Set(ref _encoderChoices, value);
    }

    public OptionChoice SelectedEncoder
    {
        get => _selectedEncoder;
        set
        {
            if (value is null)
            {
                return; // the list was replaced; the check picks again
            }

            _wantedEncoder = value.Value;
            EncoderNote = null;
            Set(ref _selectedEncoder, value);
        }
    }

    /// <summary>Says when the saved encoder did not pass the test, and what is used instead.</summary>
    public string? EncoderNote
    {
        get => _encoderNote;
        private set => Set(ref _encoderNote, value);
    }

    /// <summary>The running encoder test; done when the list is up to date.</summary>
    public Task EncodersChecked { get; private set; }

    /// <summary>Whether links will be made, from what is typed now.</summary>
    public string SharingStatus
    {
        get
        {
            if (IsDiscord)
            {
                return _discordStored || !string.IsNullOrWhiteSpace(DiscordWebhook)
                    ? Strings.Get("settings.sharing.on", Strings.Get("settings.provider.discord"))
                    : Strings.Get("settings.sharing.off");
            }

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
        ReleaseHotkeys();
        ClearErrors();
        ShareSettings current = _file.Load();
        (StorageSettings storage, CredentialPlan credential) = PlanStorage(current.Storage);
        (DiscordSettings discord, StoredCredential? discordWebhook) = PlanDiscord(current.Discord);
        RecordingSettings recording = PlanRecording(current.Recording);
        HotkeySettings hotkeys = PlanHotkeys(current.Hotkeys);
        if (HasErrors)
        {
            ShowFirstSectionWithError();
            return;
        }

        ShareSettings next = current with
        {
            Bar = current.Bar with { ShowInCaptures = ShowBarInCaptures },
            Storage = storage,
            Discord = discord,
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

            if (discordWebhook is not null)
            {
                _credentials.Write(discord.CredentialName, discordWebhook);
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

    /// <summary>A sound switch changed on the bar shows here too, so a later Save keeps it. Only the changed switch is copied.</summary>
    private void OnLiveChanged(object? sender, EventArgs e)
    {
        AudioSettings audio = _live.Current.Recording.Audio;
        AudioSettings before = _followedAudio;
        _followedAudio = audio;
        if (audio.SystemSound != before.SystemSound)
        {
            RecordSystemSound = audio.SystemSound;
        }

        if (audio.Microphone != before.Microphone)
        {
            RecordMicrophone = audio.Microphone;
        }
    }

    /// <summary>Ends following the bar's choices; the window calls it when it closes.</summary>
    public void StopFollowing() => _live.Changed -= OnLiveChanged;

    /// <summary>Closes the window and writes nothing.</summary>
    public void Cancel()
    {
        ReleaseHotkeys();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The bar's cross only hides the bar; this ends the app. Unsaved changes are dropped, as with Cancel.</summary>
    public ICommand QuitCommand => new BarCommand(_ =>
    {
        Cancel();
        QuitRequested?.Invoke(this, EventArgs.Empty);
    });

    public event EventHandler? QuitRequested;

    /// <summary>A hotkey field got focus: recording starts, so the chords the shell would take (Win+Shift+S) reach this field.</summary>
    public void HotkeyFieldFocused(HotkeyRow row)
    {
        ReleaseHotkeys();
        _recording = _binder.Record(chord => row.Chord = chord.ToString());
        row.IsListening = true;
    }

    public void HotkeyFieldLeft() => ReleaseHotkeys();

    /// <summary>Ends the recording. Safe to call at any time and more than once; the window calls it when it closes.</summary>
    public void ReleaseHotkeys()
    {
        _recording?.Dispose();
        _recording = null;
        foreach (HotkeyRow row in Hotkeys)
        {
            row.IsListening = false;
        }
    }

    private bool HasErrors =>
        new[] { EndpointError, BucketError, RegionError, PublicBaseUrlError, CredentialError, DiscordWebhookError, MaxSizeError, FrameRateError }.Any(error => error is not null)
        || Hotkeys.Any(row => row.Error is not null);

    /// <summary>Only one section is on screen, so a mistake in another one would go unseen; the window moves to it.</summary>
    private void ShowFirstSectionWithError()
    {
        bool sharing = new[] { EndpointError, BucketError, RegionError, PublicBaseUrlError, CredentialError, DiscordWebhookError }.Any(error => error is not null);
        if (sharing)
        {
            SelectedSection = SettingsSection.Sharing;
        }
        else if (Hotkeys.Any(row => row.Error is not null))
        {
            SelectedSection = SettingsSection.Hotkeys;
        }
        else if (MaxSizeError is not null || FrameRateError is not null)
        {
            SelectedSection = SettingsSection.Recording;
        }
    }

    private void ClearErrors()
    {
        EndpointError = BucketError = RegionError = PublicBaseUrlError = CredentialError = DiscordWebhookError = MaxSizeError = FrameRateError = SaveError = null;
        foreach (HotkeyRow row in Hotkeys)
        {
            row.Error = null;
        }
    }

    private static string CredentialNameFor(StorageSettings storage) =>
        string.IsNullOrWhiteSpace(storage.CredentialName) ? ShareDefaults.CredentialName : storage.CredentialName;

    private static string DiscordNameFor(DiscordSettings discord) =>
        string.IsNullOrWhiteSpace(discord.CredentialName) ? ShareDefaults.DiscordCredentialName : discord.CredentialName;

    /// <summary>The Discord settings to save and the webhook to store; null when the stored one stays as it is.</summary>
    private (DiscordSettings Discord, StoredCredential? Webhook) PlanDiscord(DiscordSettings current)
    {
        if (!IsDiscord)
        {
            return (current, null);
        }

        DiscordSettings discord = current with { CredentialName = DiscordNameFor(current) };
        string typed = DiscordWebhook.Trim();
        if (typed.Length > 0)
        {
            if (DiscordWebhookAddress.TryParse(typed, out _))
            {
                return (discord, DiscordWebhookAddress.ToCredential(typed));
            }

            DiscordWebhookError = Strings.Get("settings.error.discord");
        }
        else if (_credentials.Read(discord.CredentialName) is null)
        {
            DiscordWebhookError = Strings.Get("settings.error.missing.discord");
        }

        return (discord, null);
    }

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
        if (IsDiscord)
        {
            return (current with { Provider = Provider }, new CredentialPlan(null));
        }

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
                        EndpointError = FieldText(error, "endpoint");
                        checking = checking with { Endpoint = ValidAddress };
                        break;
                    case "Storage.Bucket":
                        BucketError = FieldText(error, "bucket");
                        checking = checking with { Bucket = ValidText };
                        break;
                    case "Storage.Region":
                        RegionError = FieldText(error, "region");
                        checking = checking with { Region = ValidText };
                        break;
                    case "Storage.PublicBaseUrl":
                        PublicBaseUrlError = FieldText(error, "publicbase");
                        checking = checking with { PublicBaseUrl = ValidAddress };
                        break;
                    default:
                        SaveError = error.Message;
                        return;
                }
            }
        }
    }

    /// <summary>The window's own words for a field; the settings.json wording of the exception stays for the bar notice.</summary>
    private static string FieldText(InvalidStorageSettingsException error, string field) => error.Problem switch
    {
        StorageProblem.Missing => Strings.Get($"settings.error.missing.{field}"),
        StorageProblem.Address => Strings.Get("settings.error.address"),
        _ => error.Message
    };

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
        // The field's own label names it, so the error stays short enough for a third of the row.
        MaxSizeError = sizeOk ? null : Strings.Get("error.setting.positive");
        FrameRateError = rateOk ? null : Strings.Get("error.setting.positive");
        AudioSettings audio = current.Audio with { SystemSound = RecordSystemSound, Microphone = RecordMicrophone };
        return sizeOk && rateOk
            ? current with
            {
                MaxSizeMegabytes = size,
                FrameRate = rate,
                Audio = audio,
                MaxWidth = SelectedResolution.Width,
                MaxHeight = SelectedResolution.Height,
                Codec = SelectedCodec.Value,
                Encoder = SelectedEncoder.Value,
                Speed = SelectedSpeed.Value
            }
            : current;
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
            Bar = chosen.GetValueOrDefault(BarAction.Show, current.Bar),
            Region = chosen.GetValueOrDefault(BarAction.Region, current.Region),
            FullScreen = chosen.GetValueOrDefault(BarAction.FullScreen, current.FullScreen),
            Window = chosen.GetValueOrDefault(BarAction.Window, current.Window),
            Record = chosen.GetValueOrDefault(BarAction.Record, current.Record)
        };
    }
}
