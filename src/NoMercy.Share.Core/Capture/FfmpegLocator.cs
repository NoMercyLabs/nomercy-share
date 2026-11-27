namespace NoMercy.Share.Core.Capture;

/// <summary>Finds the ffmpeg file: the one the settings name, else the NoMercy build, else the first one on the PATH.</summary>
public static class FfmpegLocator
{
    public static string ExecutableName => OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    /// <summary>The NoMercy ffmpeg the media server installs, then the one NoMercy Share downloads: every GPU encoder, VP9, Opus and the screen grab built in.</summary>
    public static IReadOnlyList<string> NoMercyBuilds() =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoMercy", "binaries", "ffmpeg", ExecutableName),
        Path.Combine(FfmpegDownload.DefaultFolder(), ExecutableName)
    ];

    /// <summary>The full path of ffmpeg, or null. A setting that points at nothing finds nothing, so a typo is never hidden by another ffmpeg.</summary>
    public static string? Find(string configured, string? pathVariable, IReadOnlyList<string>? preferred = null)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return File.Exists(configured) ? configured : null;
        }

        if ((preferred ?? []).FirstOrDefault(File.Exists) is { } ours)
        {
            return ours;
        }

        foreach (string folder in (pathVariable ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(folder, ExecutableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
