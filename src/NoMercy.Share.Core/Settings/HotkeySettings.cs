using NoMercy.Share.Core.Bar;

namespace NoMercy.Share.Core.Settings;

/// <summary>The global hotkey of each capture action, as chord text such as "Ctrl+PrintScreen". An empty value turns that hotkey off.</summary>
public sealed record HotkeySettings
{
    public string Region { get; init; } = ShareDefaults.HotkeyRegion;

    public string FullScreen { get; init; } = ShareDefaults.HotkeyFullScreen;

    public string Window { get; init; } = ShareDefaults.HotkeyWindow;

    public string Record { get; init; } = ShareDefaults.HotkeyRecord;

    /// <summary>The chord text of every action that has a hotkey, in a fixed order.</summary>
    public IReadOnlyDictionary<BarAction, string> Chords() => new Dictionary<BarAction, string>
    {
        [BarAction.Region] = Region,
        [BarAction.FullScreen] = FullScreen,
        [BarAction.Window] = Window,
        [BarAction.Record] = Record
    }.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).ToDictionary();
}
