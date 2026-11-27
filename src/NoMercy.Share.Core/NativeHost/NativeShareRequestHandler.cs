using System.Buffers.Binary;
using System.Text.Json;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.NativeHost;

/// <summary>Shares one picture from the extension: saves it, uploads it when a share host is set up and lists the link. It never touches the clipboard.</summary>
public sealed class NativeShareRequestHandler(
    NativeHostSettings settings, CaptureFiles files, Func<IShareUploader> currentUploader, Func<ExpirySettings> currentExpiry, RecentLinks recent)
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly string[] Modes = ["fullPage", "visible", "element"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The first PNG chunk is IHDR; the width and height sit at offsets 16 and 20.</summary>
    private const int HeaderEnd = 24;

    public async Task<NativeShareReply> HandleAsync(string origin, byte[] message, CancellationToken cancellationToken = default)
    {
        if (!settings.AllowedOrigins.Contains(origin, StringComparer.Ordinal))
        {
            return NativeShareReply.Failure("native.error.origin");
        }

        NativeShareRequest? request = Parse(message);
        if (request is not { Type: "share" })
        {
            return NativeShareReply.Failure("native.error.type");
        }

        if (!Modes.Contains(request.Mode, StringComparer.Ordinal))
        {
            return NativeShareReply.Failure("native.error.mode");
        }

        if (!TryDecode(request.PngBase64, out byte[] png))
        {
            return NativeShareReply.Failure("native.error.base64");
        }

        if (!IsPng(png))
        {
            return NativeShareReply.Failure("native.error.png");
        }

        try
        {
            return await ShareAsync(png, cancellationToken);
        }
        catch (Exception error) when (error is ShareUploadException or UnknownStorageProviderException)
        {
            return NativeShareReply.Failure("native.error.upload", error.Message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return NativeShareReply.Failure("native.error.save", error.Message);
        }
    }

    private async Task<NativeShareReply> ShareAsync(byte[] png, CancellationToken cancellationToken)
    {
        string path = files.NewPath();
        await File.WriteAllBytesAsync(path, png, cancellationToken);
        IShareUploader uploader = currentUploader();
        if (!uploader.IsConfigured)
        {
            return new NativeShareReply(true, Path: path, Local: true);
        }

        CaptureResult capture = new(
            path, "image/png", (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16)), (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20)), png.Length);
        ShareLink link = await uploader.UploadAsync(path, currentExpiry().Resolve(null), cancellationToken);
        recent.Record(link, capture);
        return new NativeShareReply(true, Link: link.Url.AbsoluteUri, Local: false);
    }

    private static NativeShareRequest? Parse(byte[] message)
    {
        try
        {
            return JsonSerializer.Deserialize<NativeShareRequest>(message, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryDecode(string? base64, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(base64))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsPng(byte[] bytes) => bytes.Length >= HeaderEnd && bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);
}
