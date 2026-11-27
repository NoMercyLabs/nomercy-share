namespace NoMercy.Share.Core.Settings;

/// <summary>How a web page is captured: which browser, how wide the page is laid out, and how long to wait for it.</summary>
public sealed record WebCaptureSettings
{
    /// <summary>The browser file to run; blank means look for Edge, then Chrome, in their standard install folders.</summary>
    public string BrowserPath { get; init; } = ShareDefaults.WebCaptureBrowserPath;

    /// <summary>The page width for the Desktop choice, in CSS pixels.</summary>
    public int DesktopWidth { get; init; } = ShareDefaults.WebCaptureDesktopWidth;

    /// <summary>The page width for the Phone choice, in CSS pixels.</summary>
    public int PhoneWidth { get; init; } = ShareDefaults.WebCapturePhoneWidth;

    /// <summary>The height of the visible area, in CSS pixels.</summary>
    public int Height { get; init; } = ShareDefaults.WebCaptureHeight;

    /// <summary>Picture pixels per CSS pixel; 2 keeps text crisp on both Desktop and Phone.</summary>
    public double ScaleFactor { get; init; } = ShareDefaults.WebCaptureScaleFactor;

    /// <summary>How long the page gets to finish loading before the capture stops.</summary>
    public int LoadTimeoutSeconds { get; init; } = ShareDefaults.WebCaptureLoadTimeoutSeconds;

    /// <summary>The wait after the load, so late layout settles before the picture is taken.</summary>
    public int SettleMilliseconds { get; init; } = ShareDefaults.WebCaptureSettleMilliseconds;

    /// <summary>The tallest picture of a full page or an element, in CSS pixels; a longer page is cut off here.</summary>
    public int MaxFullPageHeight { get; init; } = ShareDefaults.WebCaptureMaxFullPageHeight;
}
