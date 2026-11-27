using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

/// <summary>The keyboard hook's decision, with made-up key events: no hook and no key ever reaches the desktop.</summary>
public sealed class HookChordMatcherTests
{
    private const uint KeyS = 0x53;

    private const uint KeyA = 0x41;

    private const uint KeyLeftWin = 0x5B;

    private const HotkeyModifiers WinShift = HotkeyModifiers.Win | HotkeyModifiers.Shift;

    private static HookChordMatcher Matcher() =>
        new(new Dictionary<BarAction, HotkeyChord> { [BarAction.Region] = HotkeyChord.Parse("Win+Shift+S") });

    [Fact]
    public void TheChordKeyDownSwallowsAndRaisesItsAction()
    {
        HookDecision decision = Matcher().Decide(KeyS, keyUp: false, WinShift);

        Assert.True(decision.Swallow);
        Assert.Equal(BarAction.Region, decision.Action);
    }

    [Fact]
    public void TheKeyUpAfterASwallowedKeyDownIsSwallowedAndRaisesNothing()
    {
        HookChordMatcher matcher = Matcher();
        matcher.Decide(KeyS, keyUp: false, WinShift);

        HookDecision up = matcher.Decide(KeyS, keyUp: true, HotkeyModifiers.Win);

        Assert.True(up.Swallow);
        Assert.Null(up.Action);
    }

    [Fact]
    public void AKeyRepeatWhileHeldIsSwallowedButRaisesOnlyOnce()
    {
        HookChordMatcher matcher = Matcher();
        matcher.Decide(KeyS, keyUp: false, WinShift);

        HookDecision repeat = matcher.Decide(KeyS, keyUp: false, WinShift);

        Assert.True(repeat.Swallow);
        Assert.Null(repeat.Action);
    }

    [Fact]
    public void TheWinKeyUpPassesSoNoModifierStaysDown()
    {
        HookChordMatcher matcher = Matcher();
        matcher.Decide(KeyS, keyUp: false, WinShift);
        matcher.Decide(KeyS, keyUp: true, WinShift);

        HookDecision winUp = matcher.Decide(KeyLeftWin, keyUp: true, HotkeyModifiers.None);

        Assert.False(winUp.Swallow);
        Assert.Null(winUp.Action);
    }

    [Fact]
    public void TheModifierKeysThemselvesAlwaysPass()
    {
        HookChordMatcher matcher = Matcher();

        Assert.False(matcher.Decide(KeyLeftWin, keyUp: false, HotkeyModifiers.Win).Swallow);
        Assert.False(matcher.Decide(0xA0, keyUp: false, WinShift).Swallow);
    }

    [Fact]
    public void ShiftSWithoutWinPasses()
    {
        HookDecision decision = Matcher().Decide(KeyS, keyUp: false, HotkeyModifiers.Shift);

        Assert.False(decision.Swallow);
        Assert.Null(decision.Action);
    }

    [Fact]
    public void WinSWithoutShiftPasses()
    {
        Assert.False(Matcher().Decide(KeyS, keyUp: false, HotkeyModifiers.Win).Swallow);
    }

    [Fact]
    public void ExtraModifiersMakeItAnotherChordSoItPasses()
    {
        Assert.False(Matcher().Decide(KeyS, keyUp: false, WinShift | HotkeyModifiers.Ctrl).Swallow);
    }

    [Fact]
    public void AnotherKeyWithTheModifiersPasses()
    {
        Assert.False(Matcher().Decide(KeyA, keyUp: false, WinShift).Swallow);
    }

    [Fact]
    public void AKeyUpOfAKeyNeverSwallowedPasses()
    {
        Assert.False(Matcher().Decide(KeyS, keyUp: true, WinShift).Swallow);
    }

    [Fact]
    public void ThePlainSKeyAfterASwallowedChordPassesAgain()
    {
        HookChordMatcher matcher = Matcher();
        matcher.Decide(KeyS, keyUp: false, WinShift);
        matcher.Decide(KeyS, keyUp: true, WinShift);

        Assert.False(matcher.Decide(KeyS, keyUp: false, HotkeyModifiers.None).Swallow);
    }

    [Fact]
    public void RecordingSwallowsAWinChordAndHandsItOverWithoutRaisingAnAction()
    {
        HookDecision decision = HookChordMatcher.ForRecording().Decide(KeyS, keyUp: false, WinShift);

        Assert.True(decision.Swallow);
        Assert.Null(decision.Action);
        Assert.Equal(HotkeyChord.Parse("Win+Shift+S"), decision.Recorded);
    }

    [Fact]
    public void RecordingSwallowsTheKeyUpAndTheKeyRepeatButHandsOverOnlyOnce()
    {
        HookChordMatcher matcher = HookChordMatcher.ForRecording();
        matcher.Decide(KeyS, keyUp: false, WinShift);

        HookDecision repeat = matcher.Decide(KeyS, keyUp: false, WinShift);
        HookDecision up = matcher.Decide(KeyS, keyUp: true, HotkeyModifiers.Win);

        Assert.True(repeat.Swallow);
        Assert.Null(repeat.Recorded);
        Assert.True(up.Swallow);
        Assert.Null(up.Recorded);
    }

    [Fact]
    public void RecordingLetsTheWinKeyUpAndTheModifiersThemselvesPass()
    {
        HookChordMatcher matcher = HookChordMatcher.ForRecording();
        matcher.Decide(KeyS, keyUp: false, WinShift);
        matcher.Decide(KeyS, keyUp: true, WinShift);

        Assert.False(matcher.Decide(KeyLeftWin, keyUp: true, HotkeyModifiers.None).Swallow);
        Assert.False(matcher.Decide(KeyLeftWin, keyUp: false, HotkeyModifiers.Win).Swallow);
        Assert.False(matcher.Decide(0xA0, keyUp: false, WinShift).Swallow);
    }

    [Fact]
    public void RecordingLetsAKeyWithoutWinPassUntouched()
    {
        HookChordMatcher matcher = HookChordMatcher.ForRecording();

        HookDecision decision = matcher.Decide(KeyS, keyUp: false, HotkeyModifiers.Shift);

        Assert.False(decision.Swallow);
        Assert.Null(decision.Recorded);
        Assert.False(matcher.Decide(KeyS, keyUp: true, HotkeyModifiers.Shift).Swallow);
    }

    [Fact]
    public void RecordingKeepsTheCtrlAndAltThatAreHeldInTheChord()
    {
        HookDecision decision = HookChordMatcher.ForRecording().Decide(KeyA, keyUp: false, HotkeyModifiers.Win | HotkeyModifiers.Ctrl | HotkeyModifiers.Alt);

        Assert.Equal(HotkeyChord.Parse("Win+Ctrl+Alt+A"), decision.Recorded);
    }

    [Fact]
    public void ANormalMatcherNeverRecords()
    {
        Assert.Null(Matcher().Decide(KeyA, keyUp: false, WinShift).Recorded);
    }

    [Fact]
    public void ANoModifierChordIsNeverServedByTheHook()
    {
        HookChordMatcher matcher = new(new Dictionary<BarAction, HotkeyChord> { [BarAction.FullScreen] = HotkeyChord.Parse("PrintScreen") });

        Assert.False(matcher.Decide(0x2C, keyUp: false, HotkeyModifiers.None).Swallow);
    }
}
