using System.Text.Json;

namespace NoMercy.Share.Core.Settings;

/// <summary>Reads and writes settings.json. A missing or unreadable file gives the defaults.</summary>
public sealed class SettingsFile(string path)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>The settings file of the current user: the app-data folder of the platform, then "NoMercy Share".</summary>
    public static string DefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoMercy Share", "settings.json");

    public ShareSettings Load()
    {
        if (!File.Exists(path))
        {
            return new ShareSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<ShareSettings>(File.ReadAllText(path), Json) ?? new ShareSettings();
        }
        catch (JsonException)
        {
            return new ShareSettings();
        }
    }

    public void Save(ShareSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        File.Move(temp, path, overwrite: true);
    }
}
