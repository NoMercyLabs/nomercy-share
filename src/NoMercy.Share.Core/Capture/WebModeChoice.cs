namespace NoMercy.Share.Core.Capture;

/// <summary>One thing the capture window can take a picture of. The label is made here, at the display edge.</summary>
public sealed record WebModeChoice(WebCaptureKind Kind)
{
    public string Label => Strings.Get(Kind switch
    {
        WebCaptureKind.FullPage => "web.mode.full",
        WebCaptureKind.Viewport => "web.mode.viewport",
        _ => "web.mode.element"
    });
}
