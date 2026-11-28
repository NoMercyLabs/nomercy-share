using System.Diagnostics;
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
    private WebModeChoice _selectedMode;
    private WebDeviceChoice _selectedDevice;
    private string? _addressError;
    private string? _selectorError;
    private string? _failureText;
    private bool _isBusy;

    /// <param name="clipboardText">What the clipboard holds; an https address in it is put in the address field.</param>
    public WebCaptureViewModel(IWebPageCapture capture, WebCaptureSettings settings, string? clipboardText)
    {
        _capture = capture;
        Modes = [new WebModeChoice(WebCaptureKind.FullPage), new WebModeChoice(WebCaptureKind.Viewport), new WebModeChoice(WebCaptureKind.Element)];
        WebCaptureDevice[] narrowestFirst =
        [
            WebCaptureDevice.Phone, WebCaptureDevice.Tablet, WebCaptureDevice.Desktop,
            WebCaptureDevice.FullHd, WebCaptureDevice.QuadHd, WebCaptureDevice.UltraHd
        ];
        Devices = [.. narrowestFirst.Select(device => new WebDeviceChoice(device, settings.WidthOf(device)))];
        _selectedMode = Modes[0];
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

    public IReadOnlyList<WebModeChoice> Modes { get; }

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

    public WebModeChoice SelectedMode
    {
        get => _selectedMode;
        set
        {
            Set(ref _selectedMode, value);
            OnChanged(nameof(IsElementMode));
        }
    }

    public WebDeviceChoice SelectedDevice
    {
        get => _selectedDevice;
        set => Set(ref _selectedDevice, value);
    }

    /// <summary>True when the selector field shows.</summary>
    public bool IsElementMode => SelectedMode.Kind == WebCaptureKind.Element;

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
            Result = await _capture.CaptureAsync(new WebCaptureRequest(page, SelectedMode.Kind, selector, SelectedDevice.Device), running.Token);
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
