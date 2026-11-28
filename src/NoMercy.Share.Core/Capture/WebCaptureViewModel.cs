using System.Diagnostics;
using System.Windows.Input;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Capture;

/// <summary>
/// The bar's web panel: the address, what to capture and the page width. Capture checks the fields, runs the capture
/// and closes with the picture; a failure shows under its field and the panel stays open.
/// </summary>
public sealed class WebCaptureViewModel : ObservableObject
{
    private readonly IWebPageCapture _capture;
    private CancellationTokenSource? _running;
    private string _address = string.Empty;
    private string _selector = string.Empty;
    private WebCaptureKind _mode = WebCaptureKind.FullPage;
    private WebDeviceChoice _selectedDevice;
    private string? _addressError;
    private string? _selectorError;
    private string? _failureText;
    private bool _isBusy;

    /// <param name="clipboardText">What the clipboard holds; an https address in it is put in the address field.</param>
    public WebCaptureViewModel(IWebPageCapture capture, WebCaptureSettings settings, string? clipboardText)
    {
        _capture = capture;
        PickModeCommand = new BarCommand(parameter =>
        {
            if (parameter is WebCaptureKind picked)
            {
                Mode = picked;
            }
        });
        WebCaptureDevice[] narrowestFirst =
        [
            WebCaptureDevice.Phone, WebCaptureDevice.Tablet, WebCaptureDevice.Desktop,
            WebCaptureDevice.FullHd, WebCaptureDevice.QuadHd, WebCaptureDevice.UltraHd
        ];
        Devices = [.. narrowestFirst.Select(device => new WebDeviceChoice(device, settings.WidthOf(device)))];
        _selectedDevice = Devices.Single(choice => choice.Device == WebCaptureDevice.Desktop);
        if (WebAddress.TryParse(clipboardText, out Uri? copied) && WebAddress.IsHttps(copied))
        {
            _address = clipboardText!.Trim();
        }
    }

    /// <summary>Raised after a good capture or a cancel; the panel closes.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>A model whose address is filled from the clipboard; a clipboard that cannot be read leaves it empty.</summary>
    public static async Task<WebCaptureViewModel> OpenAsync(
        IWebPageCapture capture, WebCaptureSettings settings, IClipboard? clipboard, CancellationToken cancellationToken = default)
    {
        string? copied = null;
        try
        {
            copied = clipboard is null ? null : await clipboard.GetTextAsync(cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Trace.TraceWarning("Reading the clipboard for the web panel failed: {0}", error.Message);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new WebCaptureViewModel(capture, settings, copied);
    }

    /// <summary>Waits until the panel closes. Throws <see cref="OperationCanceledException"/> when it closes without a picture.</summary>
    public async Task<CaptureResult> WaitForResultAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<CaptureResult?> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CloseRequested += (_, _) => closed.TrySetResult(Result);
        using CancellationTokenRegistration stop = cancellationToken.Register(Cancel);
        return await closed.Task ?? throw new OperationCanceledException("The web page capture was cancelled.");
    }

    /// <summary>Picks the mode given as the parameter; nothing is captured.</summary>
    public ICommand PickModeCommand { get; }

    public IReadOnlyList<WebDeviceChoice> Devices { get; }

    /// <summary>The picture of the last good capture; null until then.</summary>
    public CaptureResult? Result { get; private set; }

    public string Address
    {
        get => _address;
        set => Set(ref _address, value);
    }

    public string Selector
    {
        get => _selector;
        set => Set(ref _selector, value);
    }

    /// <summary>What Capture takes a picture of; the bar's row picks it.</summary>
    public WebCaptureKind Mode
    {
        get => _mode;
        set
        {
            Set(ref _mode, value);
            OnChanged(string.Empty); // the picked flags and their screen-reader words follow it
        }
    }

    public WebDeviceChoice SelectedDevice
    {
        get => _selectedDevice;
        set => Set(ref _selectedDevice, value);
    }

    public bool IsFullPageMode => Mode == WebCaptureKind.FullPage;

    public bool IsViewportMode => Mode == WebCaptureKind.Viewport;

    /// <summary>True when the selector field shows.</summary>
    public bool IsElementMode => Mode == WebCaptureKind.Element;

    /// <summary>The screen-reader words for the picked mode.</summary>
    public string? FullPageState => Selected(IsFullPageMode);

    public string? ViewportState => Selected(IsViewportMode);

    public string? ElementState => Selected(IsElementMode);

    public string? AddressError
    {
        get => _addressError;
        private set => Set(ref _addressError, value);
    }

    public string? SelectorError
    {
        get => _selectorError;
        private set => Set(ref _selectorError, value);
    }

    /// <summary>A failure that belongs to no one field, such as a missing browser.</summary>
    public string? FailureText
    {
        get => _failureText;
        private set => Set(ref _failureText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    /// <summary>Checks the fields, then captures. With a mistake it shows the errors and does not capture.</summary>
    public async Task CaptureAsync()
    {
        if (IsBusy)
        {
            return;
        }

        AddressError = SelectorError = FailureText = null;
        if (!WebAddress.TryParse(Address, out Uri? page))
        {
            AddressError = Strings.Get("web.error.address");
        }

        if (IsElementMode && string.IsNullOrWhiteSpace(Selector))
        {
            SelectorError = Strings.Get("web.error.selector");
        }

        if (AddressError is not null || SelectorError is not null)
        {
            return;
        }

        IsBusy = true;
        using CancellationTokenSource running = new();
        _running = running;
        try
        {
            string? selector = IsElementMode ? Selector.Trim() : null;
            Result = await _capture.CaptureAsync(new WebCaptureRequest(page, Mode, selector, SelectedDevice.Device), running.Token);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            // Cancel already closed the panel.
        }
        catch (WebCaptureException error)
        {
            ShowFailure(error);
        }
        catch (Exception error)
        {
            Trace.TraceError("Web page capture failed: {0}", error);
            FailureText = Strings.Get("web.error.failed");
        }
        finally
        {
            _running = null;
            IsBusy = false;
        }
    }

    /// <summary>Stops a capture that is running and closes the panel; nothing is shared.</summary>
    public void Cancel()
    {
        _running?.Cancel();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string? Selected(bool selected) => selected ? Strings.Get("row.selected") : null;

    private void ShowFailure(WebCaptureException error)
    {
        switch (error.Field)
        {
            case WebCaptureField.Address:
                AddressError = error.Message;
                break;
            case WebCaptureField.Selector:
                SelectorError = error.Message;
                break;
            default:
                FailureText = error.Message;
                break;
        }
    }
}
