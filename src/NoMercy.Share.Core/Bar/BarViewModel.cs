using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
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
    private readonly IUrlOpener? _opener;
    private readonly IClipboard? _clipboard;
    private readonly SynchronizationContext? _context;
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
        IWebCaptureDialog? webDialog = null)
    {
        _webDialog = webDialog;
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
            [BarAction.Region] = token => CaptureAsync(CaptureKind.Region, token),
            [BarAction.Window] = token => CaptureAsync(CaptureKind.Window, token),
            [BarAction.FullScreen] = token => CaptureAsync(CaptureKind.FullScreen, token),
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

    /// <summary>Shows text the caller made, such as the hotkeys that could not be registered.</summary>
    public void ShowMessage(string text) => Notice = text;

    /// <summary>The window calls this each second while recording, so the timer redraws.</summary>
    public void RefreshElapsed() => OnChanged(nameof(ElapsedText));

    /// <summary>Runs one button. Never throws: a failure becomes a notice.</summary>
    public async Task RunAsync(BarAction action, CancellationToken cancellationToken = default)
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
            await _actions[action](cancellationToken);
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
            OnChanged(nameof(IsRecording));
            OnChanged(nameof(RecordLabel));
            OnChanged(nameof(ElapsedText));
        }
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
        OnChanged(nameof(IsRecording));
        OnChanged(nameof(RecordLabel));
        OnChanged(nameof(ElapsedText));
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

    private async Task CaptureAsync(CaptureKind kind, CancellationToken cancellationToken) =>
        await ShareAsync(await _capture.RunAsync(kind, cancellationToken), cancellationToken);

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
            _recordingStartedAt = null;
            await ShareAsync(await _recorder.StopAsync(cancellationToken), cancellationToken);
            return;
        }

        await _recorder.StartAsync(new RecordingOptions(), cancellationToken);
        _recordingStartedAt = _clock();
        await ShowNotice("notice.recording");
        if (_recorder.StartNotice is { } without)
        {
            Notice += " " + without;
        }
    }

    private static string Minutes(TimeSpan elapsed) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Max(0, elapsed.TotalMinutes):00}:{Math.Max(0, elapsed.Seconds):00}");

    private async Task ShareAsync(CaptureResult capture, CancellationToken cancellationToken)
    {
        ShareOutcome outcome = await _flow.ShareAsync(capture, cancellationToken: cancellationToken);
        Notice = Strings.Get(NoticeKeyFor(outcome));
        if (capture.IsOverCap)
        {
            Notice += " " + Strings.Get("notice.recording.overcap", MegabyteText.FromBytes(capture.SizeBytes), MegabyteText.FromBytes(capture.SizeCapBytes));
        }
    }

    private static string NoticeKeyFor(ShareOutcome outcome)
    {
        if (outcome.Link is null)
        {
            return "notice.clipboard.local";
        }

        return outcome.Copied is { Image: true, Text: false } ? "notice.clipboard" : "notice.copied";
    }
}
