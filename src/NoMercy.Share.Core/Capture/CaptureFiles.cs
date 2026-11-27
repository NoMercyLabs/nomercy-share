using System.Globalization;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Capture;

/// <summary>Decides where a new capture file goes: the captures folder from the settings, and a UTC, sortable name that is never taken yet.</summary>
public sealed class CaptureFiles(CaptureSettings settings, string picturesFolder, Func<DateTimeOffset> clock)
{
    private const string Extension = ".png";

    /// <summary>The captures folder: the setting when it is set, else "NoMercy Share" under the Pictures folder.</summary>
    public static string ResolveFolder(string? configured, string picturesFolder) =>
        string.IsNullOrWhiteSpace(configured) ? Path.Combine(picturesFolder, ShareDefaults.CaptureFolderName) : configured;

    /// <summary>The file name for a capture time: the pattern with the UTC time, characters a file name cannot hold replaced by a dash.</summary>
    public static string FileName(string pattern, DateTimeOffset when)
    {
        string name = string.Format(CultureInfo.InvariantCulture, pattern, when.UtcDateTime);
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string([.. name.Select(c => invalid.Contains(c) ? '-' : c)]) + Extension;
    }

    /// <summary>A path in the captures folder that does not exist yet. Creates the folder.</summary>
    public string NewPath()
    {
        string folder = ResolveFolder(settings.Folder, picturesFolder);
        Directory.CreateDirectory(folder);
        string name = FileName(settings.FileNamePattern, clock());
        string path = Path.Combine(folder, name);
        for (int copy = 2; File.Exists(path); copy++)
        {
            path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(name)}-{copy}{Extension}");
        }

        return path;
    }
}
