namespace NoMercy.Share.Core.Capture;

/// <summary>One page width the capture window offers, with the width from the settings.</summary>
public sealed record WebDeviceChoice(WebCaptureDevice Device, int Width)
{
    /// <summary>The short name on the pill.</summary>
    public string Label => Strings.Get($"web.device.{Device.ToString().ToLowerInvariant()}");

    /// <summary>The name with the pixel width, for the tooltip and the screen reader.</summary>
    public string Description => Strings.Get("web.device.described", Label, Width);
}
