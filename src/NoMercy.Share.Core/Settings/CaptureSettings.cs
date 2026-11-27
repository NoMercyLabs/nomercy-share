using System.Text.Json.Serialization;

namespace NoMercy.Share.Core.Settings;

/// <summary>What a full-screen capture holds.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FullScreenArea>))]
public enum FullScreenArea
{
    /// <summary>The monitor the mouse cursor is on. A click on the bar puts the cursor on the bar's monitor.</summary>
    CursorMonitor,

    /// <summary>Every monitor, as one picture.</summary>
    VirtualScreen
}

/// <summary>Where still captures are saved and how a capture runs.</summary>
public sealed record CaptureSettings
{
    /// <summary>The folder PNG files are saved in; null or blank means "NoMercy Share" under the user's Pictures folder.</summary>
    public string? Folder { get; init; }

    /// <summary>The file name without extension: a format string whose {0} is the UTC capture time.</summary>
    public string FileNamePattern { get; init; } = ShareDefaults.CaptureFileNamePattern;

    /// <summary>How long to wait after hiding the bar, so it is gone from the screen before the picture is taken.</summary>
    public int HideDelayMilliseconds { get; init; } = ShareDefaults.CaptureHideDelayMilliseconds;

    public FullScreenArea FullScreenArea { get; init; } = ShareDefaults.FullScreenArea;
}
