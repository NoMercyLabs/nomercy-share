using System.Buffers.Binary;

namespace NoMercy.Share.Core.NativeHost;

/// <summary>Reads and writes native messaging frames: a 4-byte little-endian length, then that many bytes of UTF-8 JSON.</summary>
public sealed class NativeMessageCodec(NativeHostSettings settings)
{
    private const int HeaderBytes = 4;

    private long MaxBytes => settings.MaxMessageMegabytes * 1024L * 1024L;

    /// <summary>The next frame body; null when the input ended before a frame began.</summary>
    public async Task<byte[]?> ReadAsync(Stream input, CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[HeaderBytes];
        int got = await ReadFullyAsync(input, header, cancellationToken);
        if (got == 0)
        {
            return null;
        }

        if (got < HeaderBytes)
        {
            throw new NativeMessageTruncatedException(Strings.Get("native.error.truncated"));
        }

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length > MaxBytes)
        {
            throw new NativeMessageTooLargeException(Strings.Get("native.error.toobig", settings.MaxMessageMegabytes));
        }

        byte[] body = new byte[length];
        if (await ReadFullyAsync(input, body, cancellationToken) < body.Length)
        {
            throw new NativeMessageTruncatedException(Strings.Get("native.error.truncated"));
        }

        return body;
    }

    public async Task WriteAsync(Stream output, byte[] payload, CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[HeaderBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);
        await output.WriteAsync(header, cancellationToken);
        await output.WriteAsync(payload, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private static async Task<int> ReadFullyAsync(Stream input, byte[] buffer, CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await input.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
