using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

/// <summary>Registers and releases a chord nobody uses. No key is ever sent to the desktop.</summary>
public sealed class Win32GlobalHotkeysTests
{
    private static readonly HotkeyChord Free = HotkeyChord.Parse("Ctrl+Alt+Shift+F12");

    private static Dictionary<BarAction, HotkeyChord> Chords(HotkeyChord chord) => new() { [BarAction.Region] = chord };

    [DesktopFact]
    public void AFreeChordIsRegistered()
    {
        using Win32GlobalHotkeys hotkeys = new();

        HotkeyRegistration result = Assert.Single(hotkeys.Register(Chords(Free)));

        Assert.Equal((BarAction.Region, Free, true), (result.Action, result.Chord, result.Registered));
    }

    [DesktopFact]
    public void TheSameChordFromASecondInstanceIsReportedInUse()
    {
        using Win32GlobalHotkeys first = new();
        using Win32GlobalHotkeys second = new();

        Assert.True(Assert.Single(first.Register(Chords(Free))).Registered);
        HotkeyRegistration result = Assert.Single(second.Register(Chords(Free)));

        Assert.False(result.Registered);
    }

    [DesktopFact]
    public void DisposeReleasesTheChordForTheNextInstance()
    {
        using Win32GlobalHotkeys again = new();
        using (Win32GlobalHotkeys first = new())
        {
            Assert.True(Assert.Single(first.Register(Chords(Free))).Registered);
        }

        Assert.True(Assert.Single(again.Register(Chords(Free))).Registered);
    }

    [DesktopFact]
    public void RegisteringAgainReplacesTheEarlierChords()
    {
        using Win32GlobalHotkeys hotkeys = new();
        using Win32GlobalHotkeys other = new();
        HotkeyChord next = HotkeyChord.Parse("Ctrl+Alt+Shift+F11");

        hotkeys.Register(Chords(Free));
        hotkeys.Register(Chords(next));

        Assert.True(Assert.Single(other.Register(Chords(Free))).Registered);
    }
}
