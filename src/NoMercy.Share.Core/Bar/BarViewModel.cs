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
    private readonly RecentLinks? _recent;
    private readonly ShareDeletion? _deletion;
    private readonly IUrlOpener? _opener;
    private readonly IClipboard? _clipboard;
    private readonly SynchronizationContext? _context;
    private readonly CaptureFiles? _files;
    private readonly IFolderAccess? _folderAccess;
    private readonly IFfmpegInstall? _ffmpeg;
    private readonly RecordingSoundStore? _sound;
    private BarMenu _menu;
    private CaptureKind _recordSource = CaptureKind.Region;
    private BarAsk? _ask;
    private CaptureChoice? _choice;
    private bool _accessAskDismissed;
    private bool _downloading;
    private readonly Dictionary<BarAction, Func<CancellationToken, Task>> _actions;
    private string? _notice;
    private bool _busy;
    private bool _isRecentOpen;
    private IReadOnlyList<RecentRow> _recentRows = [];
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
        ShareDeletion? deletion = null)
    {
        _deletion = deletion;
        _sound = sound;
        if (sound is not null)
        {
            sound.Changed += (_, _) =>
            {
                OnChanged(nameof(IsMicrophoneOn));
                OnChanged(nameof(IsDesktopAudioOn));
                OnChanged(nameof(MicrophoneState));
                OnChanged(nameof(DesktopAudioState));
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
        _recent = recent;
        _opener = opener;
        _clipboard = clipboard;
        _context = SynchronizationContext.Current; // the recorder fails on another thread; the window reads the bar on this one
        if (recorder is not null)
        {
            recorder.Failed += OnRecorderFailed;
        }

        _actions = new Dictionary<BarAction, Func<CancellationToken, Task>>
        {
            [BarAction.Show] = _ =>
            {
                ShowRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            },
            [BarAction.Region] = token => CaptureAsync(CaptureKind.Region, token),
            [BarAction.Window] = token => CaptureAsync(CaptureKind.Window, token),
            [BarAction.FullScreen] = token => CaptureAsync(CaptureKind.FullScreen, token),
            [BarAction.Screenshot] = _ =>
            {
                Menu = Menu == BarMenu.Screenshot ? BarMenu.None : BarMenu.Screenshot;
                return Task.CompletedTask;
            },
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
        DeleteRecentCommand = new BarCommand(parameter =>
        {
            if (parameter is RecentRow row)
            {
                AskToDelete(row.Link);
            }
        });
        AskYesCommand = new BarCommand(_ => _ = Ask?.Yes());
        AskNoCommand = new BarCommand(_ => Ask?.No());
        CopyImageCommand = new BarCommand(_ => _ = CopyImageAsync());
        ShareLinkCommand = new BarCommand(_ => _ = ShareLinkAsync());
        DismissChoiceCommand = new BarCommand(_ => Choice = null);
        SelectRecordSourceCommand = new BarCommand(parameter =>
        {
            if (parameter is CaptureKind source)
            {
                SelectRecordSource(source);
            }
        });
        StartRecordingCommand = new BarCommand(_ => _ = StartRecordingAsync());
        ToggleMicrophoneCommand = new BarCommand(_ => SaveSound(audio => audio with { Microphone = !audio.Microphone }));
        ToggleDesktopAudioCommand = new BarCommand(_ => SaveSound(audio => audio with { SystemSound = !audio.SystemSound }));
    }

    /// <summary>The second pill under the bar.</summary>
    public BarMenu Menu
    {
        get => _menu;
        private set
        {
            if (_menu != value)
            {
                Set(ref _menu, value);
                OnChanged(nameof(IsScreenshotMenuOpen));
                OnChanged(nameof(IsRecordMenuOpen));
                OnChanged(nameof(IsRecordHighlighted));
            }
        }
    }

    public bool IsScreenshotMenuOpen => Menu == BarMenu.Screenshot;

    public bool IsRecordMenuOpen => Menu == BarMenu.Record;

    public bool IsRecordArea => _recordSource == CaptureKind.Region;

    public bool IsRecordWindow => _recordSource == CaptureKind.Window;

    public bool IsRecordFullScreen => _recordSource == CaptureKind.FullScreen;

    public bool IsMicrophoneOn => _sound?.Current.Microphone ?? false;

    public bool IsDesktopAudioOn => _sound?.Current.SystemSound ?? false;

    /// <summary>Picks the <see cref="CaptureKind"/> given as the parameter as what Start records.</summary>
    public ICommand SelectRecordSourceCommand { get; }

    /// <summary>The record button carries the active accent while its menu is open or a recording runs.</summary>
    public bool IsRecordHighlighted => IsRecording || IsRecordMenuOpen;

    /// <summary>The screen-reader words for the selected target and the sound switches.</summary>
    public string? RecordAreaState => Selected(IsRecordArea);

    public string? RecordWindowState => Selected(IsRecordWindow);

    public string? RecordFullScreenState => Selected(IsRecordFullScreen);

    public string MicrophoneState => OnOff(IsMicrophoneOn);

    public string DesktopAudioState => OnOff(IsDesktopAudioOn);

    private static string? Selected(bool selected) => selected ? Strings.Get("menu.selected") : null;

    private static string OnOff(bool on) => Strings.Get(on ? "menu.on" : "menu.off");

    public ICommand StartRecordingCommand { get; }

    public ICommand ToggleMicrophoneCommand { get; }

    public ICommand ToggleDesktopAudioCommand { get; }

    public void CloseMenu() => Menu = BarMenu.None;

    /// <summary>The Start button of the record menu. Never throws: a failure becomes a notice.</summary>
    public Task StartRecordingAsync(CancellationToken cancellationToken = default) =>
        GuardAsync(BarAction.Record, () => BeginRecordingAsync(cancellationToken));

    private void SelectRecordSource(CaptureKind source)
    {
        _recordSource = source;
        OnChanged(nameof(IsRecordArea));
        OnChanged(nameof(IsRecordWindow));
        OnChanged(nameof(IsRecordFullScreen));
        OnChanged(nameof(RecordAreaState));
        OnChanged(nameof(RecordWindowState));
        OnChanged(nameof(RecordFullScreenState));
    }

    private void SaveSound(Func<AudioSettings, AudioSettings> change)
    {
        if (_sound is null)
        {
            return;
        }

        try
        {
            _sound.Save(change(_sound.Current));
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

    /// <summary>Asks to delete the <see cref="RecentRow"/> given as the command parameter.</summary>
    public ICommand DeleteRecentCommand { get; }

    /// <summary>True while the recent links list is open. The popup binds to it, so a click outside closes it.</summary>
    public bool IsRecentOpen
    {
        get => _isRecentOpen;
        set => Set(ref _isRecentOpen, value);
    }

    /// <summary>The links as of the last time the list opened, newest first.</summary>
    public IReadOnlyList<RecentRow> RecentRows
    {
        get => _recentRows;
        private set
        {
            _recentRows = value;
            OnChanged(nameof(RecentRows));
            OnChanged(nameof(IsRecentEmpty));
        }
    }

    public bool IsRecentEmpty => _recentRows.Count == 0;

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
        GuardAsync(action, () =>
        {
            if (action is not (BarAction.Screenshot or BarAction.Record))
            {
                CloseMenu(); // before the action, so no menu is up while a shot is taken
            }

            return _actions[action](cancellationToken);
        });

    private async Task GuardAsync(BarAction action, Func<Task> work)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
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
            _busy = false;
            if (_files?.TakeFallback() == true)
            {
                CheckFolderAccess();
            }

            OnRecordingChanged();
        }
    }

    private void OnRecordingChanged()
    {
        OnChanged(nameof(IsRecording));
        OnChanged(nameof(IsRecordHighlighted));
        OnChanged(nameof(RecordLabel));
        OnChanged(nameof(ElapsedText));
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
        if (_recent is null)
        {
            return ShowNotice("notice.later");
        }

        if (IsRecentOpen)
        {
            CloseRecent();
            return Task.CompletedTask;
        }

        DateTimeOffset now = _clock();
        RecentRows = [.. _recent.Load().Select(link => new RecentRow(link, now))];
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

    private void AskToDelete(RecentLink link)
    {
        if (_recent is not { } recent)
        {
            return;
        }

        CloseRecent();
        ShareDeletion? host = _deletion?.CanDeleteFromHost(link) == true ? _deletion : null;
        Ask = new BarAsk(
            Strings.Get(host is null ? "ask.delete.list" : "ask.delete.host"),
            Strings.Get("ask.delete"),
            () => DeleteRecentAsync(recent, host, link),
            () => Ask = null,
            Strings.Get("ask.cancel"));
    }

    /// <summary>The link leaves the list only after the host, when there is one to ask, has deleted the upload.</summary>
    private async Task DeleteRecentAsync(RecentLinks recent, ShareDeletion? host, RecentLink link)
    {
        Ask = null;
        try
        {
            if (host is not null)
            {
                await host.DeleteFromHostAsync(link);
            }

            recent.Remove(link);
            Notice = Strings.Get(host is null ? "notice.removed" : "notice.deleted");
        }
        catch (ShareUploadException error)
        {
            Notice = Strings.Get("notice.delete.refused", error.Message);
        }
        catch (Exception error)
        {
            Trace.TraceError("Deleting a recent link failed: {0}", error);
            Notice = Strings.Get("notice.delete.failed");
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

    private async Task CaptureAsync(CaptureKind kind, CancellationToken cancellationToken)
    {
        Choice = null;
        await ShareAsync(await _capture.RunAsync(kind, cancellationToken), cancellationToken);
    }

    private async Task CaptureWebPageAsync(CancellationToken cancellationToken)
    {
        if (_web is null)
        {
            await ShowNotice("notice.later");
            return;
        }

        CaptureResult picture = _webDialog is null
            ? await _web.CaptureAsync(new WebCaptureRequest(), cancellationToken)
            : await _webDialog.RunAsync(_web, cancellationToken);
        await ShareAsync(picture, cancellationToken);
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
            CloseMenu();
            _recordingStartedAt = null;
            await ShareAsync(await _recorder.StopAsync(cancellationToken), cancellationToken);
            return;
        }

        Menu = IsRecordMenuOpen ? BarMenu.None : BarMenu.Record;
        if (IsRecordMenuOpen)
        {
            ShowRequested?.Invoke(this, EventArgs.Empty); // the hotkey opens the menu on a hidden bar
        }
    }

    private async Task BeginRecordingAsync(CancellationToken cancellationToken)
    {
        if (_recorder is null || _recorder.IsRecording)
        {
            return;
        }

        CloseMenu();
        if (_ffmpeg is not null && !_ffmpeg.IsFound())
        {
            if (!_downloading)
            {
                Ask = new BarAsk(Strings.Get("ask.ffmpeg"), Strings.Get("ask.download"), DownloadFfmpegAsync, () => Ask = null, Strings.Get("ask.notnow"));
            }

            return;
        }

        RecordingOptions options = new(Source: _recordSource);
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
        if (_busy || Choice is not { } choice)
        {
            return;
        }

        _busy = true;
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
            _busy = false;
        }
    }

    private static string WithOverCapWarning(string notice, CaptureResult capture) => capture.IsOverCap
        ? notice + " " + Strings.Get("notice.recording.overcap", MegabyteText.FromBytes(capture.SizeBytes), MegabyteText.FromBytes(capture.SizeCapBytes))
        : notice;
}
