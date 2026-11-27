using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Hotkeys;

namespace NoMercy.Share.Platform.Windows;

/// <summary>What the keyboard hook does with one key event: swallow it, and which action it raises, if any.</summary>
internal readonly record struct HookDecision(bool Swallow, BarAction? Action, HotkeyChord? Recorded = null);

/// <summary>
/// Decides, for the Win chords the shell would not give to RegisterHotKey, whether a key event is one of them.
/// It keeps only the chord keys it swallowed, so their key-up and key repeat are swallowed too.
/// </summary>
internal sealed class HookChordMatcher(IReadOnlyDictionary<BarAction, HotkeyChord> chords)
{
    private readonly List<(BarAction Action, HotkeyModifiers Modifiers, uint Key)> _served = [.. ServedChords(chords)];
    private readonly HashSet<uint> _swallowed = [];
    private bool _recording;

    /// <summary>A matcher that serves no chord and instead hands over any chord with Win held, for the settings field.</summary>
    public static HookChordMatcher ForRecording() => new(new Dictionary<BarAction, HotkeyChord>()) { _recording = true };

    public HookDecision Decide(uint virtualKey, bool keyUp, HotkeyModifiers held)
    {
        if (keyUp)
        {
            return new HookDecision(_swallowed.Remove(virtualKey), null);
        }

        if (_swallowed.Contains(virtualKey))
        {
            return new HookDecision(true, null);
        }

        if (_recording && held.HasFlag(HotkeyModifiers.Win) && !WindowsKeyCodes.IsModifierKey(virtualKey) && WindowsKeyCodes.TryKeyName(virtualKey, out string? name))
        {
            _swallowed.Add(virtualKey);
            return new HookDecision(true, null, new HotkeyChord(held, name));
        }

        foreach ((BarAction action, HotkeyModifiers modifiers, uint key) in _served)
        {
            if (key == virtualKey && modifiers == held)
            {
                _swallowed.Add(virtualKey);
                return new HookDecision(true, action);
            }
        }

        return new HookDecision(false, null);
    }

    private static IEnumerable<(BarAction, HotkeyModifiers, uint)> ServedChords(IReadOnlyDictionary<BarAction, HotkeyChord> chords)
    {
        foreach ((BarAction action, HotkeyChord chord) in chords)
        {
            if (chord.Modifiers.HasFlag(HotkeyModifiers.Win) && WindowsKeyCodes.TryVirtualKey(chord.Key, out uint key))
            {
                yield return (action, chord.Modifiers, key);
            }
        }
    }
}
