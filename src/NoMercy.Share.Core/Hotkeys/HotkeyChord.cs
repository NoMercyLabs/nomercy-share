using System.Diagnostics.CodeAnalysis;

namespace NoMercy.Share.Core.Hotkeys;

/// <summary>A key with the modifiers held down, such as "Ctrl+PrintScreen". Stored as that text in settings.json.</summary>
public sealed record HotkeyChord(HotkeyModifiers Modifiers, string Key)
{
    private static readonly (string Name, HotkeyModifiers Modifier)[] ModifierNames =
    [
        ("Ctrl", HotkeyModifiers.Ctrl), ("Control", HotkeyModifiers.Ctrl), ("Alt", HotkeyModifiers.Alt),
        ("Shift", HotkeyModifiers.Shift), ("Win", HotkeyModifiers.Win), ("Windows", HotkeyModifiers.Win)
    ];

    private static readonly HotkeyModifiers[] TextOrder = [HotkeyModifiers.Win, HotkeyModifiers.Ctrl, HotkeyModifiers.Alt, HotkeyModifiers.Shift];

    /// <summary>Reads "Ctrl+Shift+PrintScreen". Case, spaces and the order of the modifiers do not matter.</summary>
    public static HotkeyChord Parse(string text) =>
        TryParse(text, out HotkeyChord? chord, out string? error) ? chord : throw new FormatException(error);

    public static bool TryParse(string? text, [NotNullWhen(true)] out HotkeyChord? chord, [NotNullWhen(false)] out string? error)
    {
        chord = null;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = Strings.Get("error.hotkey.empty");
            return false;
        }

        string[] parts = [.. text.Split('+').Select(part => part.Trim())];
        HotkeyModifiers modifiers = HotkeyModifiers.None;
        foreach (string part in parts[..^1])
        {
            if (!TryModifier(part, out HotkeyModifiers modifier))
            {
                error = Strings.Get("error.hotkey.modifier", part);
                return false;
            }

            if (modifiers.HasFlag(modifier))
            {
                error = Strings.Get("error.hotkey.duplicate", part);
                return false;
            }

            modifiers |= modifier;
        }

        string? key = HotkeyKeys.Find(parts[^1]);
        if (key is null)
        {
            error = Strings.Get("error.hotkey.key", parts[^1]);
            return false;
        }

        chord = new HotkeyChord(modifiers, key);
        return true;
    }

    /// <summary>The text that <see cref="Parse"/> reads back, in the order Windows writes it: Win, Ctrl, Alt, Shift, then the key.</summary>
    public sealed override string ToString()
    {
        IEnumerable<string> held = TextOrder
            .Where(modifier => Modifiers.HasFlag(modifier))
            .Select(modifier => modifier.ToString());
        return string.Join('+', held.Append(Key));
    }

    private static bool TryModifier(string text, out HotkeyModifiers modifier)
    {
        foreach ((string name, HotkeyModifiers value) in ModifierNames)
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                modifier = value;
                return true;
            }
        }

        modifier = HotkeyModifiers.None;
        return false;
    }
}
