using System.IO.Compression;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Tests;

public sealed class ScreenFrameTests
{
    /// <summary>A made-up frame: the blue byte holds x, the green byte holds y, the red byte a constant.</summary>
    private static ScreenFrame Pattern(int x, int y, int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                int at = ((row * width) + col) * 4;
                pixels[at] = (byte)col;
                pixels[at + 1] = (byte)row;
                pixels[at + 2] = 200;
                pixels[at + 3] = 255;
            }
        }

        return new ScreenFrame(x, y, width, height, pixels);
    }

    [Fact]
    public void ACropKeepsOnlyTheRectangleInVirtualScreenCoordinates()
    {
        ScreenFrame frame = Pattern(-100, 0, 40, 30);

        ScreenFrame crop = frame.Crop(new PixelRect(-90, 5, 10, 4));

        Assert.Equal((-90, 5, 10, 4), (crop.X, crop.Y, crop.Width, crop.Height));
        Assert.Equal(10 * 4 * 4, crop.Bgra.Length);
        Assert.Equal([10, 5, 200, 255], crop.Bgra[..4]);
        Assert.Equal([19, 8, 200, 255], crop.Bgra[^4..]);
    }

    [Fact]
    public void ACropOutsideTheFrameIsRefused()
    {
        ScreenFrame frame = Pattern(0, 0, 10, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => frame.Crop(new PixelRect(5, 5, 10, 10)));
    }

    [Fact]
    public void ThePngHasTheFrameSizeAndItsPixelsBack()
    {
        ScreenFrame frame = Pattern(0, 0, 7, 5);

        byte[] png = PngEncoder.Encode(frame);

        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], png[..8]);
        Assert.Equal((7, 5, 8, 2), ReadHeader(png));
        byte[] raw = Inflate(png);
        int rowLength = 1 + (7 * 3);
        Assert.Equal(5 * rowLength, raw.Length);
        // Row 3, column 4: red, green, blue after the filter byte.
        int at = (3 * rowLength) + 1 + (4 * 3);
        Assert.Equal([200, 3, 4], raw[at..(at + 3)]);
        Assert.Equal(0, raw[3 * rowLength]);
    }

    [Fact]
    public void EveryPngChunkHasAValidCrc()
    {
        byte[] png = PngEncoder.Encode(Pattern(0, 0, 3, 3));

        int offset = 8;
        int chunks = 0;
        while (offset < png.Length)
        {
            int length = ReadInt(png, offset);
            uint stored = (uint)ReadInt(png, offset + 8 + length);
            Assert.Equal(PngEncoder.Crc32(png.AsSpan(offset + 4, 4 + length)), stored);
            offset += 12 + length;
            chunks++;
        }

        Assert.Equal(3, chunks);
    }

    private static (int Width, int Height, int Depth, int ColorType) ReadHeader(byte[] png) =>
        (ReadInt(png, 16), ReadInt(png, 20), png[24], png[25]);

    private static int ReadInt(byte[] data, int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];

    private static byte[] Inflate(byte[] png)
    {
        using MemoryStream joined = new();
        int offset = 8;
        while (offset < png.Length)
        {
            int length = ReadInt(png, offset);
            if (System.Text.Encoding.ASCII.GetString(png, offset + 4, 4) == "IDAT")
            {
                joined.Write(png, offset + 8, length);
            }

            offset += 12 + length;
        }

        joined.Position = 0;
        using ZLibStream zlib = new(joined, CompressionMode.Decompress);
        using MemoryStream raw = new();
        zlib.CopyTo(raw);
        return raw.ToArray();
    }
}
