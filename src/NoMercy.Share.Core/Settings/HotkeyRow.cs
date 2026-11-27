using NoMercy.Share.Core.Bar;

namespace NoMercy.Share.Core.Settings;

/// <summary>One line of the hotkey list: an action and its chord as text. An empty chord is a hotkey that is off.</summary>
public sealed class HotkeyRow(BarAction action, string chord) : ObservableObject
{
    private string _chord = chord;
    private string? _error;

    public BarAction Action { get; } = action;

    /// <summary>The same words as the bar's tooltip for this action.</summary>
    public string Label => Strings.Get(Action switch
    {
        BarAction.Region => "bar.region",
        BarAction.Window => "bar.window",
        BarAction.FullScreen => "bar.fullscreen",
        _ => "bar.record"
    });

    public string Chord
    {
        get => _chord;
        set => Set(ref _chord, value);
    }

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public void Clear() => Chord = string.Empty;
}
