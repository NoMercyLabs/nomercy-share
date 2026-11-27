using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NoMercy.Share.Core.Tests;

/// <summary>A local HttpListener standing in for Discord. Answers come from a queue; the last one repeats.</summary>
internal sealed class FakeDiscordServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Queue<(int Status, string Body, TimeSpan Delay)> _answers = new();
    private readonly Task _loop;

    public FakeDiscordServer()
    {
        Address = new Uri($"http://127.0.0.1:{FreePort()}/");
        _listener.Prefixes.Add(Address.AbsoluteUri);
        _listener.Start();
        _loop = Task.Run(Serve);
    }

    public Uri Address { get; }

    public List<(string Method, string Path, string ContentType, string Body)> Requests { get; } = [];

    public void Answer(int status, string body = "", TimeSpan delay = default) => _answers.Enqueue((status, body, delay));

    public void Dispose()
    {
        _listener.Close();
        _loop.Wait(TimeSpan.FromSeconds(2));
    }

    private static int FreePort()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException or ArgumentException)
            {
                return;
            }

            using StreamReader reader = new(context.Request.InputStream, Encoding.UTF8);
            string body = await reader.ReadToEndAsync();
            lock (Requests)
            {
                Requests.Add((context.Request.HttpMethod, context.Request.Url!.AbsolutePath, context.Request.ContentType ?? string.Empty, body));
            }

            (int status, string answer, TimeSpan delay) = NextAnswer();
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }

            await Reply(context, status, answer);
        }
    }

    private (int Status, string Body, TimeSpan Delay) NextAnswer()
    {
        if (_answers.Count == 0)
        {
            return (204, string.Empty, TimeSpan.Zero);
        }

        return _answers.Count > 1 ? _answers.Dequeue() : _answers.Peek();
    }

    private static async Task Reply(HttpListenerContext context, int status, string answer)
    {
        context.Response.StatusCode = status;
        try
        {
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(answer));
            context.Response.Close();
        }
        catch (Exception error) when (error is HttpListenerException or ObjectDisposedException)
        {
            // the client gave up first, as in the timeout test
        }
    }
}

/// <summary>Sends every request to the fake server, so the real address checks run and the real HTTP stack carries the post.</summary>
internal sealed class RedirectToHandler(Uri target) : DelegatingHandler(new SocketsHttpHandler())
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.RequestUri = new Uri(target, request.RequestUri!.PathAndQuery);
        return base.SendAsync(request, cancellationToken);
    }
}
