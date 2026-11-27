using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace NoMercy.Share.Core.Capture;

/// <summary>Writes a <see cref="ScreenFrame"/> as a PNG (8-bit RGB, no alpha: a screen is opaque). Plain code, so every platform shares it.</summary>
public static class PngEncoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(ScreenFrame frame)
    {
        using MemoryStream png = new();
        png.Write(Signature);
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), frame.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), frame.Height);
        header[8] = 8; // bit depth
        header[9] = 2; // color type: RGB
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", Deflate(frame));
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>The CRC-32 the PNG format uses, over the chunk type and data.</summary>
    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ uint.MaxValue;
    }

    private static byte[] Deflate(ScreenFrame frame)
    {
        int rowLength = 1 + (frame.Width * 3);
        byte[] scanlines = new byte[rowLength * frame.Height];
        for (int row = 0; row < frame.Height; row++)
        {
            int to = row * rowLength;
            scanlines[to++] = 0; // filter: none
            int from = row * frame.Width * ScreenFrame.BytesPerPixel;
            for (int col = 0; col < frame.Width; col++)
            {
                scanlines[to++] = frame.Bgra[from + 2];
                scanlines[to++] = frame.Bgra[from + 1];
                scanlines[to++] = frame.Bgra[from];
                from += ScreenFrame.BytesPerPixel;
            }
        }

        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(scanlines);
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] body = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, body);
        data.CopyTo(body, 4);
        byte[] length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(body);
        byte[] crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(body));
        stream.Write(crc);
    }

    private static uint[] BuildCrcTable()
    {
        uint[] table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int bit = 0; bit < 8; bit++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
