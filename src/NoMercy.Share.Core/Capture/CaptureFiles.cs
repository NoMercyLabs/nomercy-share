using System.Globalization;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Capture;

/// <summary>Decides where a new capture file goes: the captures folder from the settings, and a UTC, sortable name that is never taken yet.</summary>
public sealed class CaptureFiles(CaptureSettings settings, string picturesFolder, Func<DateTimeOffset> clock, string? fallbackFolder = null)
{
    private const string PictureExtension = ".png";

    private readonly string _fallbackFolder = fallbackFolder ?? DefaultFallbackFolder();

    /// <summary>Where captures go when Windows refuses the captures folder: "NoMercy Share\Captures" under the local app data folder.</summary>
    public static string DefaultFallbackFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ShareDefaults.CaptureFolderName, ShareDefaults.CaptureFallbackFolderName);

    /// <summary>The captures folder: the setting when it is set, else "NoMercy Share" under the Pictures folder.</summary>
    public static string ResolveFolder(string? configured, string picturesFolder) =>
        string.IsNullOrWhiteSpace(configured) ? Path.Combine(picturesFolder, ShareDefaults.CaptureFolderName) : configured;

    /// <summary>The file name for a capture time: the pattern with the UTC time, characters a file name cannot hold replaced by a dash.</summary>
    public static string FileName(string pattern, DateTimeOffset when, string extension = PictureExtension)
    {
        string name = string.Format(CultureInfo.InvariantCulture, pattern, when.UtcDateTime);
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string([.. name.Select(c => invalid.Contains(c) ? '-' : c)]) + extension;
    }

    /// <summary>A path in the captures folder that does not exist yet. Creates the folder; uses the fallback folder when Windows will not let the app write there.</summary>
    public string NewPath(string extension = PictureExtension)
    {
        string folder = ResolveFolder(settings.Folder, picturesFolder);
        if (!CanWrite(folder))
        {
            folder = _fallbackFolder;
            Directory.CreateDirectory(folder);
        }

        string name = FileName(settings.FileNamePattern, clock(), extension);
        string path = Path.Combine(folder, name);
        for (int copy = 2; File.Exists(path); copy++)
        {
            path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(name)}-{copy}{extension}");
        }

        return path;
    }

    /// <summary>Windows' controlled folder access can refuse an unknown app a file in the Pictures folder even after the folder exists, so the check writes a file that deletes itself on close.</summary>
    private static bool CanWrite(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            using FileStream check = File.Create(Path.Combine(folder, $".write-check-{Guid.NewGuid():N}"), 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
