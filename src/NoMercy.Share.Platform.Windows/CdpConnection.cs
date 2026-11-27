using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

/// <summary>A Chrome DevTools Protocol session over one WebSocket: commands wait for their answer, events can be waited for.</summary>
internal sealed class CdpConnection : IDisposable
{
    private readonly ClientWebSocket _socket;
    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pending = [];
    private readonly Dictionary<string, List<TaskCompletionSource>> _waiters = [];
    private readonly Lock _gate = new();
    private readonly Task _reader;
    private int _nextId;

    private CdpConnection(ClientWebSocket socket)
    {
        _socket = socket;
        _reader = Task.Run(ReadAsync);
    }

    public static async Task<CdpConnection> ConnectAsync(Uri address, CancellationToken cancellationToken)
    {
        ClientWebSocket socket = new();
        try
        {
            await socket.ConnectAsync(address, cancellationToken);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return new CdpConnection(socket);
    }

    /// <summary>Sends one command and returns its "result" object.</summary>
    public async Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        TaskCompletionSource<JsonElement> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int id;
        lock (_gate)
        {
            id = ++_nextId;
            _pending[id] = answer;
        }

        byte[] message = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?> { ["id"] = id, ["method"] = method, ["params"] = parameters ?? new { } });
        await _socket.SendAsync(message, WebSocketMessageType.Text, true, cancellationToken);
        return await answer.Task.WaitAsync(cancellationToken);
    }

    /// <summary>A task that completes the next time the browser raises the event. Ask before the command that causes it.</summary>
    public Task NextEvent(string method)
    {
        TaskCompletionSource waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (!_waiters.TryGetValue(method, out List<TaskCompletionSource>? list))
            {
                _waiters[method] = list = [];
            }

            list.Add(waiter);
        }

        return waiter.Task;
    }

    public void Dispose()
    {
        _socket.Dispose();
        try
        {
            _reader.Wait(1000);
        }
        catch (AggregateException)
        {
            // The reader ends with the socket; its failure was already passed to the waiting calls.
        }
    }

    private async Task ReadAsync()
    {
        byte[] buffer = new byte[64 * 1024];
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                using MemoryStream message = new();
                WebSocketReceiveResult received;
                do
                {
                    received = await _socket.ReceiveAsync(buffer, CancellationToken.None);
                    message.Write(buffer, 0, received.Count);
                }
                while (!received.EndOfMessage);

                if (received.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                Dispatch(message.ToArray());
            }
        }
        catch (Exception error) when (error is WebSocketException or ObjectDisposedException or OperationCanceledException)
        {
            // The browser went away or the connection was disposed; every waiting call is failed below.
        }

        Fail();
    }

    private void Dispatch(byte[] message)
    {
        using JsonDocument document = JsonDocument.Parse(message);
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("id", out JsonElement id))
        {
            TaskCompletionSource<JsonElement>? answer;
            lock (_gate)
            {
                _pending.Remove(id.GetInt32(), out answer);
            }

            if (root.TryGetProperty("error", out JsonElement error))
            {
                Trace.TraceError("DevTools command failed: {0}", error);
                answer?.TrySetException(new WebCaptureException(WebCaptureField.None, Strings.Get("web.error.failed")));
            }
            else
            {
                answer?.TrySetResult(root.GetProperty("result").Clone());
            }

            return;
        }

        if (root.TryGetProperty("method", out JsonElement method))
        {
            List<TaskCompletionSource>? waiting;
            lock (_gate)
            {
                _waiters.Remove(method.GetString()!, out waiting);
            }

            waiting?.ForEach(waiter => waiter.TrySetResult());
        }
    }

    private void Fail()
    {
        lock (_gate)
        {
            foreach (TaskCompletionSource<JsonElement> answer in _pending.Values)
            {
                answer.TrySetException(new WebCaptureException(WebCaptureField.None, Strings.Get("web.error.failed")));
            }

            _pending.Clear();
        }
    }
}
