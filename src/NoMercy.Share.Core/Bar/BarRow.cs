namespace NoMercy.Share.Core.Bar;

/// <summary>What the bar's one row shows: the picked mode, the picked recording target and which groups belong to them.</summary>
public sealed class BarRow : ObservableObject
{
    public BarMode Mode => BarMode.Screenshot;

    public bool IsScreenshotMode => false;

    public bool IsRecordMode => false;

    public bool IsWebMode => false;

    public bool ShowsTargets => true;

    public bool ShowsRecordControls => true;

    public bool IsAreaPicked => false;

    public bool IsWindowPicked => false;

    public bool IsFullScreenPicked => false;
}
