using NoMercy.Share.Core.Hotkeys;

namespace NoMercy.Share.Core.Tests;

public sealed class HotkeyChordTests
{
    [Theory]
    [InlineData("Ctrl+PrintScreen")]
    [InlineData("Shift+PrintScreen")]
    [InlineData("PrintScreen")]
    [InlineData("Ctrl+Alt+Shift+F12")]
    [InlineData("Ctrl+Alt+Shift+Win+K")]
    [InlineData("Alt+PageDown")]
    public void AChordWrittenAsTextReadsBackAsTheSameText(string text)
    {
        Assert.Equal(text, HotkeyChord.Parse(text).ToString());
    }

    [Fact]
    public void ParsingKeepsTheModifiersAndTheKeyName()
    {
        HotkeyChord chord = HotkeyChord.Parse("Ctrl+Shift+PrintScreen");

        Assert.Equal(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, chord.Modifiers);
        Assert.Equal("PrintScreen", chord.Key);
    }

    [Fact]
    public void CaseSpacesAndModifierOrderDoNotMatter()
    {
        Assert.Equal(HotkeyChord.Parse("Ctrl+Shift+PrintScreen"), HotkeyChord.Parse(" shift + CONTROL + printscreen "));
        Assert.Equal("Ctrl+Alt+K", HotkeyChord.Parse("alt+ctrl+k").ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+Banana")]
    [InlineData("A+B")]
    [InlineData("Ctrl+Ctrl+K")]
    [InlineData("Ctrl++K")]
    public void TextThatIsNotAChordIsRejectedWithAMessage(string text)
    {
        Assert.False(HotkeyChord.TryParse(text, out HotkeyChord? chord, out string? error));
        Assert.Null(chord);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void ParseThrowsAFormatExceptionThatNamesTheProblem()
    {
        FormatException error = Assert.Throws<FormatException>(() => HotkeyChord.Parse("Ctrl+Banana"));

        Assert.Contains("Banana", error.Message);
    }
}
