namespace NoMercy.Share.Core.Hotkeys;

/// <summary>The keys held down with the main key of a chord.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8
}
