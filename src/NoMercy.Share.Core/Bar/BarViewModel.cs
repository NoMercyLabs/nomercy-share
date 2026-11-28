using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Bar;

/// <summary>The floating bar: each button runs one action. Recording and web page capture are optional until a platform has them.</summary>
public sealed class BarViewModel : ObservableObject
{
    private readonly CaptureFlow _capture;
    private readonly ShareFlow _flow;
    private readonly IScreenRecorder? _recorder;
    private readonly IWebPageCapture? _web;
    private readonly IWebCaptureDialog? _webDialog;
    private readonly Func<DateTimeOffset> _clock;
    private readonly IUrlOpener? _opener;
    private readonly IClipboard? _clipboard;
    private readonly SynchronizationContext? _context;
    private readonly CaptureFiles? _files;
    private readonly IFolderAccess? _folderAccess;
    private readonly IFfmpegInstall? _ffmpeg;
    private readonly RecordingSoundStore? _sound;
    private readonly LiveSettings? _live;
    private bool _isTakingScreenshot;
    private readonly LastActionStore? _lastAction;
    private readonly AreaPicker? _picker;
    private Task _running = Task.CompletedTask;
    private BarAsk? _ask;
    private CaptureChoice? _choice;
    private bool _accessAskDismissed;
    private bool _downloading;
    private CancellationTokenSource? _openWebDialog;
    private readonly Dictionary<BarAction, Func<CancellationToken, Task>> _actions;
    private string? _notice;
    private bool _isRecentOpen;
    private DateTimeOffset? _recordingStartedAt;

    public BarViewModel(
        CaptureFlow capture,
        ShareFlow flow,
        IScreenRecorder? recorder = null,
        IWebPageCapture? web = null,
        Func<DateTimeOffset>? clock = null,
        RecentLinks? recent = null,
        IUrlOpener? opener = null,
        IClipboard? clipboard = null,
        IWebCaptureDialog? webDialog = null,
        CaptureFiles? files = null,
        IFolderAccess? folderAccess = null,
        IFfmpegInstall? ffmpeg = null,
        RecordingSoundStore? sound = null,
        ShareDeletion? deletion = null,
        LastActionStore? lastAction = null,
        AreaPicker? picker = null,
        LiveSettings? live = null)
    {
        _live = live;
        if (live is not null)
        {
            live.Changed += (_, _) => OnChanged(nameof(IsBarExcludedFromCaptures));
        }

        _lastAction = lastAction;
        ReturnToLastMode();
        _picker = picker;
        Recent = new RecentList(recent, deletion);
        _sound = sound;
        if (sound is not null)
        {
            sound.Changed += (_, _) =>
            {
                OnChanged(nameof(IsMicrophoneOn));
                OnChanged(nameof(IsDesktopAudioOn));
                OnChanged(nameof(MicrophoneState));
                OnChanged(nameof(DesktopAudioState));
                OnChanged(nameof(MicrophoneTip));
                OnChanged(nameof(DesktopAudioTip));
            };
        }

        _ffmpeg = ffmpeg;
        _webDialog = webDialog;
        _files = files;
        _folderAccess = folderAccess;
        _capture = capture;
        _flow = flow;
        _recorder = recorder;
        _web = web;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _opener = opener;
        _clipboard = clipboard;
        _context = SynchronizationContext.Current; // the recorder fails on another thread; the window reads the bar on this one
        if (recorder is not null)
        {
            recorder.Failed += OnRecorderFailed;
        }

        _actions = new Dictionary<BarAction, Func<CancellationToken, Task>>
        {
            [BarAction.Show] = ShowAndRepeatAsync,
            [BarAction.Region] = token => CaptureAsync(CaptureKind.Region, token),
            [BarAction.Window] = token => CaptureAsync(CaptureKind.Window, token),
            [BarAction.FullScreen] = token => CaptureAsync(CaptureKind.FullScreen, token),
            [BarAction.Screenshot] = _ => SelectMode(BarMode.Screenshot),
            [BarAction.Record] = ToggleRecordingAsync,
            [BarAction.WebPage] = CaptureWebPageAsync,
            [BarAction.Recent] = _ => ToggleRecent(),
            [BarAction.Settings] = _ => RequestSettings(),
            [BarAction.Close] = _ => RequestClose()
        };
        ActionCommand = new BarCommand(parameter =>
        {
            if (parameter is BarAction action)
            {
                _ = RunAsync(action);
            }
        });
        CopyRecentCommand = new BarCommand(parameter =>
        {
            if (parameter is RecentRow row)
            {
                _ = CopyRecentAsync(row);
            }
        });
        OpenRecentCommand = new BarCommand(parameter =>
        {
            if (parameter is RecentRow row)
            {
                OpenRecent(row);
            }
        });
        AskYesCommand = new BarCommand(_ => _ = Ask?.Yes());
        AskNoCommand = new BarCommand(_ => Ask?.No());
        CopyImageCommand = new BarCommand(_ => _ = CopyImageAsync());
        ShareLinkCommand = new BarCommand(_ => _ = ShareLinkAsync());
        DismissChoiceCommand = new BarCommand(_ => Choice = null);
        PickTargetCommand = new BarCommand(parameter =>
        {
            if (parameter is CaptureKind target)
            {
                _ = PickTargetAsync(target);
            }
        });
        StartRecordingCommand = new BarCommand(_ => _ = StartRecordingAsync());
        ToggleMicrophoneCommand = new BarCommand(_ => SaveSound(audio => audio with { Microphone = !audio.Microphone }));
        ToggleDesktopAudioCommand = new BarCommand(_ => SaveSound(audio => audio with { SystemSound = !audio.SystemSound }));
    }

    /// <summary>The mode, the picked target and the groups the bar shows for them.</summary>
    public BarRow Row { get; } = new();

    public bool IsMicrophoneOn => _sound?.Current.Microphone ?? false;

    public bool IsDesktopAudioOn => _sound?.Current.SystemSound ?? false;

    /// <summary>The bar is kept out of captures at all times, except in a running recording with Show bar in video on and no screenshot being taken.</summary>
    public bool IsBarExcludedFromCaptures => !((_live?.Current.Recording.ShowTool ?? false) && IsRecording && !_isTakingScreenshot);

    /// <summary>A click on the target given as the parameter; see <see cref="PickTargetAsync"/>.</summary>
    public ICommand PickTargetCommand { get; }

    /// <summary>The screen-reader words for the sound switches.</summary>
    public string MicrophoneState => OnOff(IsMicrophoneOn);

    public string DesktopAudioState => OnOff(IsDesktopAudioOn);

    /// <summary>The tooltips of the sound switches: the name and On or Off.</summary>
    public string MicrophoneTip => Strings.Get("row.switch.tip", Strings.Get("row.microphone"), MicrophoneState);

    public string DesktopAudioTip => Strings.Get("row.switch.tip", Strings.Get("row.desktopaudio"), DesktopAudioState);

    private static string OnOff(bool on) => Strings.Get(on ? "row.on" : "row.off");

    public ICommand StartRecordingCommand { get; }

    public ICommand ToggleMicrophoneCommand { get; }

    public ICommand ToggleDesktopAudioCommand { get; }

    /// <summary>In record mode a target is only picked for Start; in screenshot mode it captures at once.</summary>
    public Task PickTargetAsync(CaptureKind target)
    {
        if (Row.IsRecordMode)
        {
            Row.RecordSource = target;
            return Task.CompletedTask;
        }

        return RunAsync(target switch
        {
            CaptureKind.Window => BarAction.Window,
            CaptureKind.FullScreen => BarAction.FullScreen,
            _ => BarAction.Region
        });
    }

    /// <summary>The bar opens in the mode of the last action, or for a screenshot when there is none.</summary>
    public void ReturnToLastMode() => Row.Mode = _lastAction?.Load().Task switch
    {
        LastActionTask.Recording => BarMode.Record,
        LastActionTask.WebPage => BarMode.Web,
        _ => BarMode.Screenshot
    };

    /// <summary>Escape on the bar closes the recent list, else cancels a waiting pick, else hides the bar.</summary>
    public void Escape()
    {
        if (IsRecentOpen)
        {
            CloseRecent();
        }
        else if (!CancelWhatWaitsForTheUser())
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The Start button. Never throws: a failure becomes a notice.</summary>
    public Task StartRecordingAsync(CancellationToken cancellationToken = default) =>
        GuardAsync(BarAction.Record, () => BeginRecordingAsync(cancellationToken));

    private Task SelectMode(BarMode mode)
    {
        Row.Mode = mode;
        return Task.CompletedTask;
    }

    private void SaveSound(Func<AudioSettings, AudioSettings> change)
    {
        try
        {
            _sound?.Save(change(_sound.Current));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Notice = Strings.Get("settings.save.failed", error.Message);
        }
    }

    public ICommand CopyImageCommand { get; }

    public ICommand ShareLinkCommand { get; }

    public ICommand DismissChoiceCommand { get; }

    /// <summary>The capture that waits for "copy image" or "copy link" under the bar, or null.</summary>
    public CaptureChoice? Choice
    {
        get => _choice;
        private set => Set(ref _choice, value);
    }

    /// <summary>The yes button of the question under the bar.</summary>
    public ICommand AskYesCommand { get; }

    /// <summary>The Not now button of the question under the bar.</summary>
    public ICommand AskNoCommand { get; }

    /// <summary>The lasting yes-or-no question under the bar, or null. It stays until one of its buttons is pressed.</summary>
    public BarAsk? Ask
    {
        get => _ask;
        private set => Set(ref _ask, value);
    }

    /// <summary>Asks to let the app write in the captures folder when it is not writable, the system protection is on and the user did not say Not now.</summary>
    public void CheckFolderAccess()
    {
        if (_files is null || _folderAccess is null || _accessAskDismissed || _ask is not null)
        {
            return;
        }

        if (_folderAccess.IsProtectionOn() && !_files.CanWriteCapturesFolder())
        {
            Ask = new BarAsk(Strings.Get("ask.folder", _files.CapturesFolder), Strings.Get("ask.allow"), () => AllowAccessAsync(), DismissAccessAsk, Strings.Get("ask.notnow"));
        }
    }

    /// <summary>Not now on the folder question: it is not asked again this run.</summary>
    public void DismissAccessAsk()
    {
        _accessAskDismissed = true;
        Ask = null;
    }

    /// <summary>Downloads the NoMercy ffmpeg, with its progress on the bar, then says it is ready for the next recording.</summary>
    private async Task DownloadFfmpegAsync()
    {
        Ask = null;
        _downloading = true;
        Notice = Strings.Get("notice.ffmpeg.progress", 0);
        try
        {
            await _ffmpeg!.DownloadAsync(new BarProgress(share => Post(() =>
            {
                if (_downloading)
                {
                    Notice = Strings.Get("notice.ffmpeg.progress", (int)(share * 100));
                }
            })));
            _downloading = false;
            Notice = Strings.Get("notice.ffmpeg.ready");
        }
        catch (Exception error) when (error is IOException or HttpRequestException or System.Text.Json.JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            _downloading = false;
            Notice = Strings.Get("notice.ffmpeg.failed", error.Message);
        }
    }

    /// <summary>Runs on the bar's thread when there is one; a test has none and runs it at once.</summary>
    private void Post(Action action)
    {
        if (_context is null)
        {
            action();
        }
        else
        {
            _context.Post(_ => action(), null);
        }
    }

    private sealed class BarProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    /// <summary>Runs the system's allow request, then says whether captures save to the folder again.</summary>
    public async Task AllowAccessAsync(CancellationToken cancellationToken = default)
    {
        if (_files is null || _folderAccess is null)
        {
            return;
        }

        Ask = null;
        bool granted = false;
        try
        {
            granted = await _folderAccess.RequestAccessAsync(cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Trace.TraceError("Asking for folder access failed: {0}", error);
        }

        Notice = granted && _files.CanWriteCapturesFolder()
            ? Strings.Get("ask.allowed", _files.CapturesFolder)
            : Strings.Get("ask.declined", _files.FallbackFolder);
    }

    /// <summary>Runs the <see cref="BarAction"/> given as the command parameter.</summary>
    public ICommand ActionCommand { get; }

    /// <summary>Copies the link of the <see cref="RecentRow"/> given as the command parameter.</summary>
    public ICommand CopyRecentCommand { get; }

    /// <summary>Opens the link of the <see cref="RecentRow"/> given as the command parameter in the browser.</summary>
    public ICommand OpenRecentCommand { get; }

    /// <summary>True while the recent links list is open. The popup binds to it, so a click outside closes it.</summary>
    public bool IsRecentOpen
    {
        get => _isRecentOpen;
        set
        {
            if (!value)
            {
                Recent.ResetQuestions();
            }

            Set(ref _isRecentOpen, value);
        }
    }

    public WebCaptureViewModel? WebPanel => null;

    /// <summary>The links as of the last time the list opened.</summary>
    public RecentList Recent { get; }

    public bool IsRecording => _recorder?.IsRecording ?? false;

    /// <summary>The tooltip and screen-reader name of the record button: start, or stop while recording.</summary>
    public string RecordLabel => Strings.Get(IsRecording ? "bar.record.stop" : "bar.record");

    /// <summary>How long the recording has run as mm:ss (minutes go past 59), or null when not recording.</summary>
    public string? ElapsedText => IsRecording && _recordingStartedAt is { } started ? Minutes(_clock() - started) : null;

    /// <summary>A short message under the bar, or null. The window clears it after a few seconds.</summary>
    public string? Notice
    {
        get => _notice;
        private set => Set(ref _notice, value);
    }

    public event EventHandler? CloseRequested;

    /// <summary>Raised when the settings button is pressed; the window opens the settings window. With no listener the button says "later".</summary>
    public event EventHandler? SettingsRequested;

    public void ClearNotice() => Notice = null;

    public void CloseRecent() => IsRecentOpen = false;

    /// <summary>The user wants the bar on screen, to pick what to capture.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>Shows text the caller made, such as the hotkeys that could not be registered.</summary>
    public void ShowMessage(string text) => Notice = text;

    /// <summary>The window calls this each second while recording, so the timer redraws.</summary>
    public void RefreshElapsed() => OnChanged(nameof(ElapsedText));

    /// <summary>Runs one button. Never throws: a failure becomes a notice.</summary>
    public Task RunAsync(BarAction action, CancellationToken cancellationToken = default) =>
        GuardAsync(action, () => _actions[action](cancellationToken));

    private async Task GuardAsync(BarAction action, Func<Task> work)
    {
        if (action != BarAction.Show && CancelWhatWaitsForTheUser())
        {
            await _running;
        }

        if (!_running.IsCompleted)
        {
            return;
        }

        TaskCompletionSource finished = new();
        _running = finished.Task;
        ClearNotice();
        if (action != BarAction.Recent)
        {
            CloseRecent();
        }

        try
        {
            await work();
        }
        catch (OperationCanceledException)
        {
            // The user backed out of the capture; nothing to say.
        }
        catch (ShareUploadException error)
        {
            Notice = Strings.Get("notice.upload.failed", error.Message);
        }
        catch (RecordingException error)
        {
            Notice = Strings.Get("notice.recording.failed", error.Message);
        }
        catch (WebCaptureException error)
        {
            Notice = error.Message;
        }
        catch (Exception error)
        {
            Trace.TraceError("Bar action {0} failed: {1}", action, error);
            Notice = Strings.Get("notice.failed");
        }
        finally
        {
            if (_files?.TakeFallback() == true)
            {
                CheckFolderAccess();
            }

            OnRecordingChanged();
            finished.SetResult();
        }
    }

    private void OnRecordingChanged()
    {
        Row.IsRecording = IsRecording;
        OnChanged(nameof(IsRecording));
        OnChanged(nameof(RecordLabel));
        OnChanged(nameof(ElapsedText));
        OnChanged(nameof(IsBarExcludedFromCaptures));
    }

    private void OnRecorderFailed(object? sender, string reason)
    {
        if (_context is null || SynchronizationContext.Current == _context)
        {
            ShowRecordingFailed(reason);
        }
        else
        {
            _context.Post(_ => ShowRecordingFailed(reason), null);
        }
    }

    private void ShowRecordingFailed(string reason)
    {
        _recordingStartedAt = null;
        Notice = Strings.Get("notice.recording.failed", reason);
        OnRecordingChanged();
    }

    private Task ShowNotice(string key)
    {
        Notice = Strings.Get(key);
        return Task.CompletedTask;
    }

    private Task ToggleRecent()
    {
        if (IsRecentOpen)
        {
            CloseRecent();
            return Task.CompletedTask;
        }

        if (!Recent.TryLoad(_clock()))
        {
            return ShowNotice("notice.later");
        }

        IsRecentOpen = true;
        return Task.CompletedTask;
    }

    private async Task CopyRecentAsync(RecentRow row)
    {
        CloseRecent();
        try
        {
            if (_clipboard is not null)
            {
                await _clipboard.SetTextAsync(row.Link.Url);
                Notice = Strings.Get("notice.copied");
            }
        }
        catch (Exception error)
        {
            Trace.TraceError("Copying a recent link failed: {0}", error);
            Notice = Strings.Get("notice.failed");
        }
    }

    private void OpenRecent(RecentRow row)
    {
        CloseRecent();
        try
        {
            _opener?.Open(new Uri(row.Link.Url));
        }
        catch (Exception error)
        {
            Trace.TraceError("Opening a recent link failed: {0}", error);
            Notice = Strings.Get("notice.failed");
        }
    }

    private Task RequestSettings()
    {
        if (SettingsRequested is null)
        {
            return ShowNotice("notice.later");
        }

        SettingsRequested.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private Task RequestClose()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <summary>The bar hotkey: shows the bar and starts the last action again; while recording it only shows the bar.</summary>
    private Task ShowAndRepeatAsync(CancellationToken cancellationToken)
    {
        ShowRequested?.Invoke(this, EventArgs.Empty);
        if (IsRecording)
        {
            return Task.CompletedTask;
        }

        LastAction last = _lastAction?.Load() ?? new LastAction();
        return last.Task switch
        {
            LastActionTask.WebPage => CaptureWebPageAsync(cancellationToken),
            LastActionTask.Recording => RepeatRecordingAsync(last.Source, cancellationToken),
            _ => CaptureAsync(last.Source, cancellationToken)
        };
    }

    private Task RepeatRecordingAsync(CaptureKind source, CancellationToken cancellationToken)
    {
        Row.RecordSource = source;

        // Without a picker there is no moment to change one's mind, so Start stays the user's press.
        return source == CaptureKind.Region ? BeginRecordingAsync(cancellationToken) : SelectMode(BarMode.Record);
    }

    private async Task CaptureAsync(CaptureKind kind, CancellationToken cancellationToken)
    {
        Row.Mode = BarMode.Screenshot;
        _lastAction?.Save(new LastAction(LastActionTask.Screenshot, kind));
        Choice = null;
        await ShareAsync(await TakeScreenshotAsync(kind, cancellationToken), cancellationToken);
    }

    private async Task<CaptureResult> TakeScreenshotAsync(CaptureKind kind, CancellationToken cancellationToken)
    {
        SetTakingScreenshot(true);
        try
        {
            return await _capture.RunAsync(kind, cancellationToken);
        }
        finally
        {
            SetTakingScreenshot(false);
        }
    }

    private void SetTakingScreenshot(bool taking)
    {
        _isTakingScreenshot = taking;
        OnChanged(nameof(IsBarExcludedFromCaptures));
    }

    private async Task CaptureWebPageAsync(CancellationToken cancellationToken)
    {
        if (_web is null)
        {
            await ShowNotice("notice.later");
            return;
        }

        Row.Mode = BarMode.Web;
        _lastAction?.Save(new LastAction(LastActionTask.WebPage));
        CaptureResult picture = _webDialog is null
            ? await _web.CaptureAsync(new WebCaptureRequest(), cancellationToken)
            : await RunWebDialogAsync(_webDialog, _web, cancellationToken);
        await ShareAsync(picture, cancellationToken);
    }

    private async Task<CaptureResult> RunWebDialogAsync(IWebCaptureDialog dialog, IWebPageCapture web, CancellationToken cancellationToken)
    {
        using CancellationTokenSource open = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _openWebDialog = open;
        try
        {
            return await dialog.RunAsync(web, open.Token);
        }
        finally
        {
            _openWebDialog = null;
        }
    }

    /// <summary>Cancels an open area picker or web page window, so another choice can run; false when neither is open.</summary>
    private bool CancelWhatWaitsForTheUser()
    {
        if (_picker?.Cancel() == true)
        {
            return true;
        }

        if (_openWebDialog is not { IsCancellationRequested: false } open)
        {
            return false;
        }

        open.Cancel();
        return true;
    }

    private async Task ToggleRecordingAsync(CancellationToken cancellationToken)
    {
        if (_recorder is null)
        {
            await ShowNotice("notice.later");
            return;
        }

        if (_recorder.IsRecording)
        {
            _recordingStartedAt = null;
            CaptureResult video = await _recorder.StopAsync(cancellationToken);
            OnRecordingChanged(); // before the sharing, which can take long
            await ShareAsync(video, cancellationToken);
            return;
        }

        Row.Mode = BarMode.Record;
        ShowRequested?.Invoke(this, EventArgs.Empty); // the hotkey shows a hidden bar
    }

    private async Task BeginRecordingAsync(CancellationToken cancellationToken)
    {
        if (_recorder is null || _recorder.IsRecording)
        {
            return;
        }

        Row.Mode = BarMode.Record;
        _lastAction?.Save(new LastAction(LastActionTask.Recording, Row.RecordSource));
        if (_ffmpeg is not null && !_ffmpeg.IsFound())
        {
            if (!_downloading)
            {
                Ask = new BarAsk(Strings.Get("ask.ffmpeg"), Strings.Get("ask.download"), DownloadFfmpegAsync, () => Ask = null, Strings.Get("ask.notnow"));
            }

            return;
        }

        RecordingOptions options = new(Source: Row.RecordSource);
        if (options.Source == CaptureKind.Window)
        {
            // The window to record is the one that was active before the bar, as for a window screenshot.
            await _capture.WhileHiddenAsync(async () =>
            {
                await _recorder.StartAsync(options, cancellationToken);
                return true;
            }, cancellationToken);
        }
        else
        {
            await _recorder.StartAsync(options, cancellationToken);
        }

        _recordingStartedAt = _clock();
        await ShowNotice("notice.recording");
        if (_recorder.StartNotice is { } without)
        {
            Notice += " " + without;
        }
    }

    private static string Minutes(TimeSpan elapsed) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Max(0, elapsed.TotalMinutes):00}:{Math.Max(0, elapsed.Seconds):00}");

    /// <summary>With a share host the capture waits for the user's choice; without one there is only the clipboard.</summary>
    private async Task ShareAsync(CaptureResult capture, CancellationToken cancellationToken)
    {
        if (_flow.CanShare)
        {
            Choice = new CaptureChoice(capture);
            return;
        }

        await _flow.ShareAsync(capture, cancellationToken: cancellationToken);
        Notice = WithOverCapWarning(Strings.Get("notice.clipboard.local"), capture);
    }

    /// <summary>The picture itself goes on the clipboard; nothing is uploaded.</summary>
    public Task CopyImageAsync() => ResolveChoiceAsync(async capture =>
    {
        await _flow.CopyImageAsync(capture);
        return "notice.picture.copied";
    });

    /// <summary>Uploads the capture and puts its link on the clipboard.</summary>
    public Task ShareLinkAsync() => ResolveChoiceAsync(async capture =>
    {
        Notice = Strings.Get("notice.uploading");
        await _flow.CopyLinkAsync(capture);
        return "notice.copied";
    });

    /// <summary>Runs one answer to the choice. A failure keeps the choice, so the user can try again or pick the other.</summary>
    private async Task ResolveChoiceAsync(Func<CaptureResult, Task<string>> answer)
    {
        if (!_running.IsCompleted || Choice is not { } choice)
        {
            return;
        }

        TaskCompletionSource finished = new();
        _running = finished.Task;
        try
        {
            string done = await answer(choice.Capture);
            Choice = null;
            Notice = WithOverCapWarning(Strings.Get(done), choice.Capture);
        }
        catch (ShareUploadException error)
        {
            Notice = Strings.Get("notice.upload.failed", error.Message);
        }
        catch (Exception error)
        {
            Trace.TraceError("The capture choice failed: {0}", error);
            Notice = Strings.Get("notice.failed");
        }
        finally
        {
            finished.SetResult();
        }
    }

    private static string WithOverCapWarning(string notice, CaptureResult capture) => capture.IsOverCap
        ? notice + " " + Strings.Get("notice.recording.overcap", MegabyteText.FromBytes(capture.SizeBytes), MegabyteText.FromBytes(capture.SizeCapBytes))
        : notice;
}
