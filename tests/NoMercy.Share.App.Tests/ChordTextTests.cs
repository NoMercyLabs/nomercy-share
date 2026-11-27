using Avalonia.Input;

namespace NoMercy.Share.App.Tests;

public sealed class ChordTextTests
{
    [Theory]
    [InlineData(Key.Snapshot, KeyModifiers.Control, "Ctrl+PrintScreen")]
    [InlineData(Key.F9, KeyModifiers.Control | KeyModifiers.Shift, "Ctrl+Shift+F9")]
    [InlineData(Key.D5, KeyModifiers.None, "5")]
    [InlineData(Key.K, KeyModifiers.Alt | KeyModifiers.Meta, "Win+Alt+K")]
    [InlineData(Key.S, KeyModifiers.Meta | KeyModifiers.Shift, "Win+Shift+S")]
    [InlineData(Key.Return, KeyModifiers.None, "Enter")]
    [InlineData(Key.Next, KeyModifiers.None, "PageDown")]
    [InlineData(Key.Scroll, KeyModifiers.None, "ScrollLock")]
    public void AKeyWithItsModifiersBecomesTheChordTextTheSettingsRead(Key key, KeyModifiers modifiers, string expected)
    {
        Assert.Equal(expected, ChordText.From(key, modifiers));
    }

    [Theory]
    [InlineData(Key.LeftCtrl, KeyModifiers.Control)]
    [InlineData(Key.RightShift, KeyModifiers.Shift)]
    [InlineData(Key.LWin, KeyModifiers.Meta)]
    [InlineData(Key.OemTilde, KeyModifiers.None)]
    public void AModifierAloneOrAKeyNoChordCanUseGivesNothing(Key key, KeyModifiers modifiers)
    {
        Assert.Null(ChordText.From(key, modifiers));
    }
}
