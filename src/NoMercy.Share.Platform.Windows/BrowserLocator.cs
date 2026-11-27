namespace NoMercy.Share.Platform.Windows;

/// <summary>Finds the browser file: the one from the settings, else Edge, then Chrome, in their standard install folders.</summary>
internal static class BrowserLocator
{
    public static string? Find(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return File.Exists(configured) ? configured : null;
        }

        return Candidates().FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> Candidates()
    {
        string[] programFiles = [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)];
        foreach (string folder in programFiles.Where(folder => folder.Length > 0))
        {
            yield return Path.Combine(folder, "Microsoft", "Edge", "Application", "msedge.exe");
        }

        string[] chromeFolders = [.. programFiles, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)];
        foreach (string folder in chromeFolders.Where(folder => folder.Length > 0))
        {
            yield return Path.Combine(folder, "Google", "Chrome", "Application", "chrome.exe");
        }
    }
}
