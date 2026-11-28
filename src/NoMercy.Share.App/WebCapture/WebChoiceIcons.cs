using Avalonia.Data.Converters;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>The Moooom icon of each choice in the web capture window.</summary>
internal static class WebChoiceIcons
{
    public static readonly FuncValueConverter<WebCaptureKind, string> Mode = new(kind => kind switch
    {
        WebCaptureKind.FullPage => "fileLines",
        WebCaptureKind.Viewport => "macOsWindow",
        _ => "crossHair"
    });

    public static readonly FuncValueConverter<WebCaptureDevice, string> Device =
        new(device => device is WebCaptureDevice.Phone or WebCaptureDevice.Tablet ? "mobilePhone" : "monitor");
}
