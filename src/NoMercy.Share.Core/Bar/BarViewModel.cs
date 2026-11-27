using System.Windows.Input;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Bar;

/// <summary>The floating bar: each button runs one action through the platform interfaces.</summary>
public sealed class BarViewModel : ObservableObject
{
    private readonly CaptureFlow _capture;
    private readonly IScreenRecorder _recorder;
    private readonly IWebPageCapture _web;
    private readonly ShareFlow _flow;
    private string? _notice;
    private bool _busy;

    public BarViewModel(CaptureFlow capture, IScreenRecorder recorder, IWebPageCapture web, ShareFlow flow)
    {
        _capture = capture;
        _recorder = recorder;
        _web = web;
        _flow = flow;
        RegionCommand = Command(BarAction.Region);
        WindowCommand = Command(BarAction.Window);
        FullScreenCommand = Command(BarAction.FullScreen);
        RecordCommand = Command(BarAction.Record);
        WebPageCommand = Command(BarAction.WebPage);
        RecentCommand = Command(BarAction.Recent);
        SettingsCommand = Command(BarAction.Settings);
        CloseCommand = Command(BarAction.Close);
    }

    public ICommand RegionCommand { get; }

    public ICommand WindowCommand { get; }

    public ICommand FullScreenCommand { get; }

    public ICommand RecordCommand { get; }

    public ICommand WebPageCommand { get; }

    public ICommand RecentCommand { get; }

    public ICommand SettingsCommand { get; }

    public ICommand CloseCommand { get; }

    public bool IsRecording => _recorder.IsRecording;

    /// <summary>The tooltip and screen-reader name of the record button: start, or stop while recording.</summary>
    public string RecordLabel => Strings.Get(IsRecording ? "bar.record.stop" : "bar.record");

    /// <summary>A short message under the bar, or null. The window clears it after a few seconds.</summary>
    public string? Notice
    {
        get => _notice;
        private set => Set(ref _notice, value);
    }

    public event EventHandler? CloseRequested;

    public void ClearNotice() => Notice = null;

    /// <summary>Runs one button. Never throws: an action that is not built yet or fails becomes a notice.</summary>
    public async Task RunAsync(BarAction action, CancellationToken cancellationToken = default)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            await DoAsync(action, cancellationToken);
        }
        catch (NotImplementedException)
        {
            Notice = Strings.Get("notice.later");
        }
        catch (OperationCanceledException)
        {
            // The user backed out of the capture; nothing to say.
        }
        catch (Exception)
        {
            Notice = Strings.Get("notice.failed");
        }
        finally
        {
            _busy = false;
            OnChanged(nameof(IsRecording));
            OnChanged(nameof(RecordLabel));
        }
    }

    private BarCommand Command(BarAction action) => new(() => _ = RunAsync(action));

    private async Task DoAsync(BarAction action, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case BarAction.Region:
                await CaptureAsync(CaptureKind.Region, cancellationToken);
                break;
            case BarAction.Window:
                await CaptureAsync(CaptureKind.Window, cancellationToken);
                break;
            case BarAction.FullScreen:
                await CaptureAsync(CaptureKind.FullScreen, cancellationToken);
                break;
            case BarAction.WebPage:
                await ShareAsync(await _web.CaptureAsync(new WebCaptureRequest(), cancellationToken), cancellationToken);
                break;
            case BarAction.Record:
                await ToggleRecordingAsync(cancellationToken);
                break;
            case BarAction.Recent:
            case BarAction.Settings:
                Notice = Strings.Get("notice.later");
                break;
            case BarAction.Close:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>A still capture ends on the clipboard; sharing it is a later step.</summary>
    private async Task CaptureAsync(CaptureKind kind, CancellationToken cancellationToken)
    {
        await _capture.RunAsync(kind, cancellationToken);
        Notice = Strings.Get("notice.clipboard");
    }

    private async Task ToggleRecordingAsync(CancellationToken cancellationToken)
    {
        if (_recorder.IsRecording)
        {
            await ShareAsync(await _recorder.StopAsync(cancellationToken), cancellationToken);
            return;
        }

        await _recorder.StartAsync(new RecordingOptions(), cancellationToken);
        Notice = Strings.Get("notice.recording");
    }

    private async Task ShareAsync(CaptureResult capture, CancellationToken cancellationToken)
    {
        await _flow.ShareAsync(capture, cancellationToken: cancellationToken);
        Notice = Strings.Get("notice.copied");
    }
}

/// <summary>A command that runs a plain action.</summary>
public sealed class BarCommand(Action run) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => run();
}
