using System.Text.Json.Serialization;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Bar;

/// <summary>What the user can start from the bar and have the bar hotkey start again.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LastActionTask>))]
public enum LastActionTask
{
    Screenshot,
    Recording,
    WebPage
}

/// <summary>What the user started last. <see cref="Source"/> says what a screenshot or a recording took; a web page ignores it.</summary>
public sealed record LastAction(
    LastActionTask Task = ShareDefaults.LastActionTask,
    [property: JsonConverter(typeof(JsonStringEnumConverter<CaptureKind>))] CaptureKind Source = ShareDefaults.LastActionSource);
