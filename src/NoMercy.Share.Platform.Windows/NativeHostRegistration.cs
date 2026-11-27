using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using NoMercy.Share.Core.NativeHost;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Tells Edge and Chrome where the app is, so the extension can start it: a manifest file and one default value per browser, in HKCU only.</summary>
[SupportedOSPlatform("windows")]
public sealed class NativeHostRegistration(string dataFolder, string exePath, NativeHostSettings settings, string softwareKey = "Software")
{
    private static readonly string[] BrowserKeys = [@"Microsoft\Edge", @"Google\Chrome"];

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private string ManifestPath => Path.Combine(dataFolder, "native-host", $"{ShareDefaults.NativeHostName}.json");

    /// <summary>Writes what differs from what is there. True when something was written.</summary>
    public bool Register()
    {
        bool wrote = WriteManifest();
        foreach (string browser in BrowserKeys)
        {
            wrote |= WriteRegistryDefault($@"{softwareKey}\{browser}\NativeMessagingHosts\{ShareDefaults.NativeHostName}");
        }

        return wrote;
    }

    private bool WriteManifest()
    {
        string content = JsonSerializer.Serialize(new HostManifest(ShareDefaults.NativeHostName, "NoMercy Share", exePath, "stdio", settings.AllowedOrigins), Json);
        if (File.Exists(ManifestPath) && File.ReadAllText(ManifestPath) == content)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
        File.WriteAllText(ManifestPath, content);
        return true;
    }

    private bool WriteRegistryDefault(string subKey)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey);
        if (key.GetValue(null) as string == ManifestPath)
        {
            return false;
        }

        key.SetValue(null, ManifestPath);
        return true;
    }

    private sealed record HostManifest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("allowed_origins")] IReadOnlyList<string> AllowedOrigins);
}
