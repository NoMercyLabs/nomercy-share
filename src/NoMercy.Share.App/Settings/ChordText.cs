using Avalonia.Input;
using NoMercy.Share.Core.Hotkeys;

namespace NoMercy.Share.App;

/// <summary>Turns a key press in the hotkey field into the chord text that settings.json holds.</summary>
public static class ChordText
{
    /// <summary>The chord text, or null for a modifier alone or a key no hotkey can use.</summary>
    public static string? From(Key key, KeyModifiers modifiers)
    {
        string? name = KeyName(key);
        if (name is null || HotkeyKeys.Find(name) is not { } known)
        {
            return null;
        }

        HotkeyModifiers held = HotkeyModifiers.None;
        held |= modifiers.HasFlag(KeyModifiers.Control) ? HotkeyModifiers.Ctrl : HotkeyModifiers.None;
        held |= modifiers.HasFlag(KeyModifiers.Alt) ? HotkeyModifiers.Alt : HotkeyModifiers.None;
        held |= modifiers.HasFlag(KeyModifiers.Shift) ? HotkeyModifiers.Shift : HotkeyModifiers.None;
        held |= modifiers.HasFlag(KeyModifiers.Meta) ? HotkeyModifiers.Win : HotkeyModifiers.None;
        return new HotkeyChord(held, known).ToString();
    }

    private static string? KeyName(Key key) => key switch
    {
        Key.Snapshot => "PrintScreen",
        Key.Return => "Enter",
        Key.Scroll => "ScrollLock",
        Key.Next => "PageDown",
        Key.Prior => "PageUp",
        >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => key.ToString()
    };
}
