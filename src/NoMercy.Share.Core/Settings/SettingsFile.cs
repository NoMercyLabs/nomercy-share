using System.Text.Json;

namespace NoMercy.Share.Core.Settings;

/// <summary>Reads and writes settings.json. A missing or unreadable file gives the defaults.</summary>
public sealed class SettingsFile(string path)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Names a folder that holds settings.json and recent.json instead of the user's own (a portable copy or a test run).</summary>
    public const string DataFolderVariable = "NOMERCY_SHARE_DATA";

    /// <summary>The settings file of the current user, or of the folder in NOMERCY_SHARE_DATA when it is set.</summary>
    public static string DefaultPath() => DefaultPath(Environment.GetEnvironmentVariable(DataFolderVariable));

    /// <summary>settings.json in the given data folder; with none, the app-data folder of the platform, then "NoMercy Share".</summary>
    public static string DefaultPath(string? dataFolder) =>
        Path.Combine(string.IsNullOrWhiteSpace(dataFolder) ? UserDataFolder() : dataFolder, "settings.json");

    private static string UserDataFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoMercy Share");

    public ShareSettings Load()
    {
        if (!File.Exists(path))
        {
            return new ShareSettings();
        }

        try
        {
            string text = File.ReadAllText(path);
            return KeepTheVideoSwitch(FreeTheBarChord(JsonSerializer.Deserialize<ShareSettings>(text, Json) ?? new ShareSettings()), text);
        }
        catch (JsonException)
        {
            return new ShareSettings();
        }
    }

    /// <summary>Files from before the bar hotkey gave its chord to the region capture; the bar takes it and the region gets its default.</summary>
    private static ShareSettings FreeTheBarChord(ShareSettings settings) =>
        Hotkeys.HotkeyChord.TryParse(settings.Hotkeys.Bar, out Hotkeys.HotkeyChord? bar, out _)
        && Hotkeys.HotkeyChord.TryParse(settings.Hotkeys.Region, out Hotkeys.HotkeyChord? region, out _)
        && bar == region
            ? settings with { Hotkeys = settings.Hotkeys with { Region = ShareDefaults.HotkeyRegion } }
            : settings;

    /// <summary>Files from before Bar.ShowInCaptures held the choice for videos only, as Recording.ShowTool; it carries over.</summary>
    private static ShareSettings KeepTheVideoSwitch(ShareSettings settings, string text)
    {
        using JsonDocument file = JsonDocument.Parse(text);
        return IsOn(file.RootElement, "Recording", "ShowTool") && !Has(file.RootElement, "Bar", nameof(BarSettings.ShowInCaptures))
            ? settings with { Bar = settings.Bar with { ShowInCaptures = true } }
            : settings;
    }

    private static bool Has(JsonElement root, string section, string key) => Find(root, section, key) is not null;

    private static bool IsOn(JsonElement root, string section, string key) => Find(root, section, key) is { ValueKind: JsonValueKind.True };

    private static JsonElement? Find(JsonElement root, string section, string key) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(section, out JsonElement group)
        && group.ValueKind == JsonValueKind.Object && group.TryGetProperty(key, out JsonElement value)
            ? value
            : null;

    public void Save(ShareSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        File.Move(temp, path, overwrite: true);
    }
}
