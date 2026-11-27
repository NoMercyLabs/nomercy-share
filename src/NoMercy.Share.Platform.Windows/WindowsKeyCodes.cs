using NoMercy.Share.Core.Hotkeys;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Maps the key names of a chord to Windows virtual-key codes and modifier flags.</summary>
internal static class WindowsKeyCodes
{
    private const uint FirstFunctionKey = 0x70;

    private static readonly Dictionary<string, uint> Named = new(StringComparer.Ordinal)
    {
        ["PrintScreen"] = 0x2C,
        ["Pause"] = 0x13,
        ["ScrollLock"] = 0x91,
        ["Space"] = 0x20,
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Escape"] = 0x1B,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28
    };

    /// <summary>The virtual-key code of a key name from <see cref="HotkeyKeys"/>, or false for a name Windows has no key for.</summary>
    public static bool TryVirtualKey(string name, out uint code)
    {
        if (Named.TryGetValue(name, out code))
        {
            return true;
        }

        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0]))
        {
            code = char.ToUpperInvariant(name[0]); // letters and digits use their ASCII code
            return true;
        }

        if (name[0] == 'F' && uint.TryParse(name.AsSpan(1), out uint number) && number is >= 1 and <= 24)
        {
            code = FirstFunctionKey + number - 1;
            return true;
        }

        code = 0;
        return false;
    }

    /// <summary>The key name for a virtual-key code, the reverse of <see cref="TryVirtualKey"/>; false for a key no chord can use.</summary>
    public static bool TryKeyName(uint code, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? name)
    {
        name = Named.FirstOrDefault(pair => pair.Value == code).Key;
        if (name is not null)
        {
            return true;
        }

        if (code is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            name = ((char)code).ToString();
        }
        else if (code is >= FirstFunctionKey and < FirstFunctionKey + 24)
        {
            name = $"F{code - FirstFunctionKey + 1}";
        }

        return name is not null;
    }

    /// <summary>Shift, Ctrl, Alt and the Win keys, in their left, right and general forms.</summary>
    public static bool IsModifierKey(uint code) => code is >= 0x10 and <= 0x12 or >= 0xA0 and <= 0xA5 or 0x5B or 0x5C;

    public static uint Modifiers(HotkeyModifiers modifiers) =>
        (modifiers.HasFlag(HotkeyModifiers.Alt) ? NativeMethods.ModAlt : 0)
        | (modifiers.HasFlag(HotkeyModifiers.Ctrl) ? NativeMethods.ModControl : 0)
        | (modifiers.HasFlag(HotkeyModifiers.Shift) ? NativeMethods.ModShift : 0)
        | (modifiers.HasFlag(HotkeyModifiers.Win) ? NativeMethods.ModWin : 0);
}
