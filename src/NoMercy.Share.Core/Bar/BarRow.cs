using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Bar;

/// <summary>What the bar's one row shows: the picked mode, the picked recording target and which groups belong to them.</summary>
public sealed class BarRow : ObservableObject
{
    private BarMode _mode;
    private CaptureKind _recordSource = CaptureKind.Region;
    private CaptureKind _screenshotSource = CaptureKind.Region;
    private bool _isRecording;

    public BarMode Mode
    {
        get => _mode;
        internal set => Update(ref _mode, value);
    }

    /// <summary>What Start records.</summary>
    public CaptureKind RecordSource
    {
        get => _recordSource;
        internal set => Update(ref _recordSource, value);
    }

    /// <summary>What the last screenshot took.</summary>
    public CaptureKind ScreenshotSource
    {
        get => _screenshotSource;
        internal set => Update(ref _screenshotSource, value);
    }

    internal bool IsRecording
    {
        set => Update(ref _isRecording, value);
    }

    public bool IsScreenshotMode => Mode == BarMode.Screenshot;

    public bool IsRecordMode => Mode == BarMode.Record;

    public bool IsWebMode => Mode == BarMode.Web;

    /// <summary>The video button carries the active accent while it is the picked mode or a recording runs.</summary>
    public bool IsRecordHighlighted => _isRecording || IsRecordMode;

    /// <summary>The sound switches and Start; a running recording cannot be changed, so they go.</summary>
    public bool ShowsRecordControls => IsRecordMode && !_isRecording;

    public bool ShowsTargets => IsScreenshotMode || ShowsRecordControls;

    /// <summary>The marked target: what Start records, or in screenshot mode what the last screenshot took.</summary>
    public bool IsAreaPicked => IsPicked(CaptureKind.Region);

    public bool IsWindowPicked => IsPicked(CaptureKind.Window);

    public bool IsFullScreenPicked => IsPicked(CaptureKind.FullScreen);

    /// <summary>The screen-reader words for the picked mode and target.</summary>
    public string? ScreenshotState => Selected(IsScreenshotMode);

    public string? RecordState => Selected(IsRecordMode);

    public string? WebState => Selected(IsWebMode);

    public string? AreaState => Selected(IsAreaPicked);

    public string? WindowState => Selected(IsWindowPicked);

    public string? FullScreenState => Selected(IsFullScreenPicked);

    private bool IsPicked(CaptureKind target) => ShowsTargets && target == (IsRecordMode ? RecordSource : ScreenshotSource);

    private static string? Selected(bool selected) => selected ? Strings.Get("row.selected") : null;

    private void Update<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnChanged(string.Empty); // every other property is computed from the fields
    }
}
