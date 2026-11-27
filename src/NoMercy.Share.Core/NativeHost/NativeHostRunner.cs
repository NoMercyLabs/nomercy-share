namespace NoMercy.Share.Core.NativeHost;

/// <summary>One native messaging exchange: read one request frame, write one reply frame.</summary>
public sealed class NativeHostRunner(NativeMessageCodec codec, NativeShareRequestHandler handler)
{
    public async Task RunAsync(string origin, Stream input, Stream output, CancellationToken cancellationToken = default)
    {
        NativeShareReply reply;
        try
        {
            byte[]? message = await codec.ReadAsync(input, cancellationToken);
            reply = message is null ? NativeShareReply.Failure("native.error.empty") : await handler.HandleAsync(origin, message, cancellationToken);
        }
        catch (Exception error) when (error is NativeMessageTooLargeException or NativeMessageTruncatedException)
        {
            reply = new NativeShareReply(false, Error: error.Message);
        }

        await codec.WriteAsync(output, reply.ToJson(), cancellationToken);
    }
}
