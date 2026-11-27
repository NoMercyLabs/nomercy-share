using System.Diagnostics;
using System.Text.Json;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Recent;

/// <summary>
/// The links shared so far, newest first, in recent.json next to settings.json. An expired link stays listed for
/// <see cref="RecentSettings.KeepExpiredDays"/>; the list holds at most <see cref="RecentSettings.MaxEntries"/>.
/// </summary>
public sealed class RecentLinks(string path, RecentSettings settings, Func<DateTimeOffset>? clock = null)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Lock _gate = new();

    /// <summary>The recent.json of the current user, in the folder of settings.json.</summary>
    public static string DefaultPath() => InFolderOf(SettingsFile.DefaultPath());

    /// <summary>recent.json next to the settings.json of the given data folder.</summary>
    public static string DefaultPath(string? dataFolder) => InFolderOf(SettingsFile.DefaultPath(dataFolder));

    private static string InFolderOf(string settingsPath) => Path.Combine(Path.GetDirectoryName(settingsPath)!, "recent.json");

    /// <summary>The kept links, newest first. A missing file is an empty list; a corrupt one is copied to recent.json.bak first.</summary>
    public IReadOnlyList<RecentLink> Load()
    {
        lock (_gate)
        {
            return Current();
        }
    }

    /// <summary>Adds a link and writes the list. Throws when the file cannot be written; the old file stays whole.</summary>
    public void Add(RecentLink link)
    {
        lock (_gate)
        {
            List<RecentLink> links = [link, .. Current()];
            Write([.. links.OrderByDescending(entry => entry.CreatedAt).Where(Kept).Take(Math.Max(0, settings.MaxEntries))]);
        }
    }

    /// <summary>Records an upload that just succeeded. A list that cannot be written never fails the share.</summary>
    public void Record(ShareLink link, CaptureResult capture)
    {
        RecentKind kind = capture.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? RecentKind.Video : RecentKind.Image;
        string? file = File.Exists(capture.FilePath) ? capture.FilePath : null;
        try
        {
            Add(new RecentLink(link.Url.AbsoluteUri, kind, capture.ContentType, capture.SizeBytes, _clock(), link.ExpiresAt, file));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError("The recent links list could not be written: {0}", error.Message);
        }
    }

    private List<RecentLink> Current()
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            List<RecentLink>? links = JsonSerializer.Deserialize<List<RecentLink>>(File.ReadAllText(path), Json);
            return [.. (links ?? []).OrderByDescending(entry => entry.CreatedAt).Where(Kept)];
        }
        catch (JsonException)
        {
            File.Copy(path, path + ".bak", overwrite: true);
            return [];
        }
    }

    private bool Kept(RecentLink link) => link.ExpiresAt is not { } expiresAt || expiresAt.AddDays(settings.KeepExpiredDays) > _clock();

    private void Write(List<RecentLink> links)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(links, Json));
        File.Move(temp, path, overwrite: true);
    }
}
