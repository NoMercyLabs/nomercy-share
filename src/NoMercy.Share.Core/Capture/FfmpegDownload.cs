using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Capture;

/// <summary>Downloads the latest NoMercy FFmpeg release for this PC. The zip must match both GitHub's checksum and the release
/// manifest, or it is thrown away; only ffmpeg is unpacked into the folder.</summary>
public sealed class FfmpegDownload(HttpClient http, string folder, string releaseUrl, string platform, Func<bool> isFound) : IFfmpegInstall
{
    private const string ManifestName = "manifest.json";

    private const string DigestPrefix = "sha256:";

    private const int BufferBytes = 1 << 20;

    /// <summary>The NoMercy Share copy: "NoMercy Share\ffmpeg" under the local app data folder.</summary>
    public static string DefaultFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ShareDefaults.CaptureFolderName, ShareDefaults.FfmpegFolderName);

    /// <summary>The release's platform word for this PC, as in ffmpeg-9.1-windows-x86_64-v1.0.50.zip.</summary>
    public static string ThisPlatform() => RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "windows-aarch64" : "windows-x86_64";

    public bool IsFound() => isFound();

    public async Task DownloadAsync(IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        using JsonDocument release = JsonDocument.Parse(await http.GetStringAsync(releaseUrl, cancellationToken));
        JsonElement[] assets = [.. release.RootElement.GetProperty("assets").EnumerateArray()];
        JsonElement zip = assets.FirstOrDefault(asset => Name(asset).Contains($"-{platform}-", StringComparison.Ordinal) && Name(asset).EndsWith(".zip", StringComparison.Ordinal));
        if (zip.ValueKind == JsonValueKind.Undefined)
        {
            throw new IOException(Strings.Get("error.ffmpeg.noasset", platform));
        }

        string expected = await ExpectedSha256Async(zip, assets, cancellationToken);
        string download = Path.Combine(Path.GetTempPath(), $"nomercy-ffmpeg-{Guid.NewGuid():N}.zip");
        try
        {
            string actual = await SaveAsync(zip.GetProperty("browser_download_url").GetString()!, download, zip.GetProperty("size").GetInt64(), progress, cancellationToken);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(Strings.Get("error.ffmpeg.checksum"));
            }

            Unpack(download);
            progress.Report(1);
        }
        finally
        {
            File.Delete(download);
        }
    }

    private static string Name(JsonElement asset) => asset.GetProperty("name").GetString() ?? string.Empty;

    /// <summary>GitHub's own digest of the asset; the release manifest, when there is one, must say the same.</summary>
    private async Task<string> ExpectedSha256Async(JsonElement zip, JsonElement[] assets, CancellationToken cancellationToken)
    {
        string digest = zip.TryGetProperty("digest", out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
        if (!digest.StartsWith(DigestPrefix, StringComparison.Ordinal))
        {
            throw new IOException(Strings.Get("error.ffmpeg.checksum"));
        }

        string expected = digest[DigestPrefix.Length..];
        JsonElement manifest = assets.FirstOrDefault(asset => Name(asset) == ManifestName);
        if (manifest.ValueKind != JsonValueKind.Undefined)
        {
            using JsonDocument listed = JsonDocument.Parse(await http.GetStringAsync(manifest.GetProperty("browser_download_url").GetString(), cancellationToken));
            string? manifestSha = listed.RootElement.GetProperty("assets").EnumerateArray()
                .Where(asset => Name(asset) == Name(zip)).Select(asset => asset.GetProperty("sha256").GetString()).FirstOrDefault();
            if (!string.Equals(manifestSha, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(Strings.Get("error.ffmpeg.checksum"));
            }
        }

        return expected;
    }

    /// <summary>Streams the file to disk, reporting progress and hashing as it goes; returns the hex SHA-256.</summary>
    private async Task<string> SaveAsync(string url, string path, long size, IProgress<double> progress, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream target = File.Create(path);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[BufferBytes];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hash.AppendData(buffer, 0, read);
            done += read;
            progress.Report(size > 0 ? Math.Min(0.99, (double)done / size) : 0);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Takes ffmpeg out of the zip; written beside its final name first, so a broken unpack never leaves half a file.</summary>
    private void Unpack(string zipPath)
    {
        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        ZipArchiveEntry entry = zip.Entries.FirstOrDefault(item => item.Name.Equals(FfmpegLocator.ExecutableName, StringComparison.OrdinalIgnoreCase))
            ?? throw new IOException(Strings.Get("error.ffmpeg.noasset", platform));
        Directory.CreateDirectory(folder);
        string final = Path.Combine(folder, FfmpegLocator.ExecutableName);
        string partial = final + ".part";
        entry.ExtractToFile(partial, overwrite: true);
        File.Move(partial, final, overwrite: true);
    }
}
