namespace NoMercy.Share.Core.Capture;

/// <summary>Finds the ffmpeg file: the one the settings name, else the first one on the PATH.</summary>
public static class FfmpegLocator
{
    public static string ExecutableName => OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    /// <summary>The full path of ffmpeg, or null. A setting that points at nothing finds nothing, so a typo is never hidden by another ffmpeg.</summary>
    public static string? Find(string configured, string? pathVariable)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return File.Exists(configured) ? configured : null;
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
