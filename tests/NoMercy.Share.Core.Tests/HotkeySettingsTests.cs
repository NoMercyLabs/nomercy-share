using System.Text.Json;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class HotkeySettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-hotkeys-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheDefaultsAreTheShareXFamiliarChords()
    {
        IReadOnlyDictionary<BarAction, string> chords = new ShareSettings().Hotkeys.Chords();

        Assert.Equal("Win+Shift+S", chords[BarAction.Region]);
        Assert.Equal("PrintScreen", chords[BarAction.FullScreen]);
        Assert.Equal("Alt+PrintScreen", chords[BarAction.Window]);
        Assert.Equal("Shift+PrintScreen", chords[BarAction.Record]);
        Assert.Equal(4, chords.Count);
    }

    [Fact]
    public void AnEmptyValueTurnsThatHotkeyOff()
    {
        IReadOnlyDictionary<BarAction, string> chords = new HotkeySettings { Record = "", Window = "  " }.Chords();

        Assert.False(chords.ContainsKey(BarAction.Record));
        Assert.False(chords.ContainsKey(BarAction.Window));
        Assert.Equal("PrintScreen", chords[BarAction.FullScreen]);
    }

    [Fact]
    public void AChordInTheSettingsBeatsTheDefault()
    {
        Assert.Equal("Ctrl+Alt+R", new HotkeySettings { Region = "Ctrl+Alt+R" }.Chords()[BarAction.Region]);
    }

    [Fact]
    public void AFileWithoutAHotkeysSectionGetsWinShiftSForRegion()
    {
        string path = Path.Combine(_folder, "settings.json");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(path, "{}");

        Assert.Equal("Win+Shift+S", new SettingsFile(path).Load().Hotkeys.Chords()[BarAction.Region]);
    }

    [Fact]
    public void AFileThatNamesTheOldDefaultKeepsIt()
    {
        string path = Path.Combine(_folder, "settings.json");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(path, JsonSerializer.Serialize(new { Hotkeys = new { Region = "Ctrl+PrintScreen" } }));

        Assert.Equal("Ctrl+PrintScreen", new SettingsFile(path).Load().Hotkeys.Chords()[BarAction.Region]);
    }

    [Fact]
    public void AHotkeyInTheFileIsReadAndTheRestKeepTheirDefaults()
    {
        string path = Path.Combine(_folder, "settings.json");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(path, JsonSerializer.Serialize(new { Hotkeys = new { Region = "Ctrl+Shift+S", Record = "" } }));

        IReadOnlyDictionary<BarAction, string> chords = new SettingsFile(path).Load().Hotkeys.Chords();

        Assert.Equal("Ctrl+Shift+S", chords[BarAction.Region]);
        Assert.False(chords.ContainsKey(BarAction.Record));
        Assert.Equal("PrintScreen", chords[BarAction.FullScreen]);
    }
}
