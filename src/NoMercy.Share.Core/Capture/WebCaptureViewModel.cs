using System.Diagnostics;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Capture;

/// <summary>
/// The "Capture a web page" window: the address, what to capture and the page width. Capture checks the fields, runs the capture
/// and closes with the picture; a failure shows under its field and the window stays open.
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
        Devices = [new WebDeviceChoice(WebCaptureDevice.Desktop, settings.DesktopWidth), new WebDeviceChoice(WebCaptureDevice.Phone, settings.PhoneWidth)];
        _selectedMode = Modes[0];
        _selectedDevice = Devices[0];
        if (WebAddress.TryParse(clipboardText, out Uri? copied) && WebAddress.IsHttps(copied))
        {
            _address = clipboardText!.Trim();
        }
    }

    /// <summary>Raised after a good capture or a cancel; the window closes.</summary>
    public event EventHandler? CloseRequested;

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
            // Cancel already closed the window.
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

    /// <summary>Stops a capture that is running and closes the window; nothing is shared.</summary>
    public void Cancel()
    {
        StopRunning();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops a capture that is running, for a window that was closed some other way.</summary>
    public void StopRunning() => _running?.Cancel();

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
