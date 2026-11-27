namespace NoMercy.Share.Core.Capture;

/// <summary>One page width the capture window offers, with the width from the settings.</summary>
public sealed record WebDeviceChoice(WebCaptureDevice Device, int Width)
{
    public string Label => Strings.Get(Device == WebCaptureDevice.Phone ? "web.device.phone" : "web.device.desktop", Width);
}
