using NoMercy.Share.Core.NativeHost;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class NativeMessageCodecTests
{
    private static NativeMessageCodec Codec(int megabytes = 64) => new(new NativeHostSettings { MaxMessageMegabytes = megabytes });

    [Fact]
    public async Task AFrameRoundTrips()
    {
        MemoryStream stream = new();
        byte[] payload = "{\"type\":\"share\"}"u8.ToArray();

        await Codec().WriteAsync(stream, payload);
        stream.Position = 0;

        Assert.Equal(payload, await Codec().ReadAsync(stream));
    }

    [Fact]
    public async Task TheLengthPrefixIsFourBytesLittleEndian()
    {
        MemoryStream stream = new();

        await Codec().WriteAsync(stream, new byte[0x0102]);

        byte[] written = stream.ToArray();
        Assert.Equal(4 + 0x0102, written.Length);
        Assert.Equal([0x02, 0x01, 0x00, 0x00], written[..4]);
    }

    [Fact]
    public async Task AFrameBiggerThanTheSettingIsRejectedBeforeItsBodyIsRead()
    {
        MemoryStream stream = new([.. BitConverter.GetBytes(2 * 1024 * 1024), 1, 2, 3]);

        await Assert.ThrowsAsync<NativeMessageTooLargeException>(() => Codec(megabytes: 1).ReadAsync(stream));
    }

    [Fact]
    public async Task AFrameThatEndsInsideItsBodyIsRejected()
    {
        MemoryStream stream = new([.. BitConverter.GetBytes(10), 1, 2, 3]);

        await Assert.ThrowsAsync<NativeMessageTruncatedException>(() => Codec().ReadAsync(stream));
    }

    [Fact]
    public async Task AHeaderThatEndsInsideItsFourBytesIsRejected()
    {
        MemoryStream stream = new([1, 0]);

        await Assert.ThrowsAsync<NativeMessageTruncatedException>(() => Codec().ReadAsync(stream));
    }

    [Fact]
    public async Task AnEmptyInputGivesNoMessage()
    {
        Assert.Null(await Codec().ReadAsync(new MemoryStream()));
    }

    [Fact]
    public void TheDefaultLimitIsSixtyFourMegabytes()
    {
        Assert.Equal(64, new NativeHostSettings().MaxMessageMegabytes);
        Assert.Equal(64, new ShareSettings().NativeHost.MaxMessageMegabytes);
    }
}
