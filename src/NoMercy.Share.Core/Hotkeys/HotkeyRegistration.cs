using NoMercy.Share.Core.Bar;

namespace NoMercy.Share.Core.Hotkeys;

/// <summary>What happened to one chord. Registered is false when another app or the system already holds it.</summary>
public sealed record HotkeyRegistration(BarAction Action, HotkeyChord Chord, bool Registered);
