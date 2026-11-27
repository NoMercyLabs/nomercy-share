using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Bar;

/// <summary>The floating bar: each button runs one action. Recording and web page capture are optional until a platform has them.</summary>
public sealed class BarViewModel : ObservableObject
{
    private readonly CaptureFlow _capture;
    private readonly ShareFlow _flow;
    private readonly IScreenRecorder? _recorder;
    private readonly IWebPageCapture? _web;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<BarAction, Func<CancellationToken, Task>> _actions;
    private string? _notice;
    private bool _busy;
    private DateTimeOffset? _recordingStartedAt;

    public BarViewModel(CaptureFlow capture, ShareFlow flow, IScreenRecorder? recorder = null, IWebPageCapture? web = null, Func<DateTimeOffset>? clock = null)
    {
        _capture = capture;
        _flow = flow;
        _recorder = recorder;
        _web = web;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _actions = new Dictionary<BarAction, Func<CancellationToken, Task>>
        {
            [BarAction.Region] = token => CaptureAsync(CaptureKind.Region, token),
            [BarAction.Window] = token => CaptureAsync(CaptureKind.Window, token),
            [BarAction.FullScreen] = token => CaptureAsync(CaptureKind.FullScreen, token),
            [BarAction.Record] = ToggleRecordingAsync,
            [BarAction.WebPage] = CaptureWebPageAsync,
            [BarAction.Recent] = _ => ShowNotice("notice.later"),
            [BarAction.Settings] = _ => ShowNotice("notice.later"),
            [BarAction.Close] = _ => RequestClose()
        };
        ActionCommand = new BarCommand(parameter =>
        {
            if (parameter is BarAction action)
            {
                _ = RunAsync(action);
            }
        });
    }

    /// <summary>Runs the <see cref="BarAction"/> given as the command parameter.</summary>
    public ICommand ActionCommand { get; }

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

    public void ClearNotice() => Notice = null;

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

    private Task ShowNotice(string key)
    {
        Notice = Strings.Get(key);
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

        await ShareAsync(await _web.CaptureAsync(new WebCaptureRequest(), cancellationToken), cancellationToken);
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
    }

    private static string Minutes(TimeSpan elapsed) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Max(0, elapsed.TotalMinutes):00}:{Math.Max(0, elapsed.Seconds):00}");

    private async Task ShareAsync(CaptureResult capture, CancellationToken cancellationToken)
    {
        ShareOutcome outcome = await _flow.ShareAsync(capture, cancellationToken: cancellationToken);
        Notice = Strings.Get(NoticeKeyFor(outcome));
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
