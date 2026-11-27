using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Hotkeys;

/// <summary>Registers the chords of the settings and runs the bar action of a pressed chord, the same path as a click on its button.</summary>
public sealed class HotkeyBinder(IGlobalHotkeys hotkeys, BarViewModel bar, HotkeySettings settings) : IDisposable
{
    private HotkeySettings _settings = settings;

    public void Start()
    {
        Dictionary<BarAction, HotkeyChord> chords = [];
        List<string> notices = [];
        foreach ((BarAction action, string text) in _settings.Chords())
        {
            if (HotkeyChord.TryParse(text, out HotkeyChord? chord, out string? error))
            {
                chords[action] = chord;
            }
            else
            {
                notices.Add(Strings.Get("notice.hotkey.invalid", text, error));
            }
        }

        hotkeys.Pressed -= OnPressed;
        hotkeys.Pressed += OnPressed;
        string[] taken = [.. hotkeys.Register(chords).Where(result => !result.Registered).Select(result => result.Chord.ToString()).Distinct()];
        if (taken.Length > 0)
        {
            notices.Add(Strings.Get(taken.Length == 1 ? "notice.hotkey.taken" : "notice.hotkey.taken.many", string.Join(", ", taken)));
        }

        if (notices.Count > 0)
        {
            bar.ShowMessage(string.Join(" ", notices));
        }
    }

    /// <summary>Registers the chords of new settings in place of the old ones.</summary>
    public void Apply(HotkeySettings next)
    {
        _settings = next;
        Start();
    }

    public void Dispose()
    {
        hotkeys.Pressed -= OnPressed;
        hotkeys.Dispose();
    }

    private void OnPressed(object? sender, BarAction action) => _ = bar.RunAsync(action);
}
