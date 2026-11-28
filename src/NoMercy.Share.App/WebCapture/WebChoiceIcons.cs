using Avalonia.Data.Converters;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>The Moooom icon of each choice in the web page panel.</summary>
internal static class WebChoiceIcons
{
    public static readonly FuncValueConverter<WebCaptureDevice, string> Device =
        new(device => device is WebCaptureDevice.Phone or WebCaptureDevice.Tablet ? "mobilePhone" : "monitor");
}
