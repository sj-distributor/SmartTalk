using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Fakes;

/// <summary>
/// Test double for the client-side WebSocket. Uses channels to simulate inbound messages
/// and captures outbound messages for assertions.
/// </summary>
public class FakeWebSocket : WebSocket
{
    private WebSocketState _state = WebSocketState.Open;
    private readonly Channel<(byte[] Data, WebSocketMessageType Type)> _inbound = Channel.CreateUnbounded<(byte[], WebSocketMessageType)>();
    private readonly ConcurrentQueue<(byte[] Data, WebSocketMessageType Type, bool EndOfMessage)> _sent = new();

    public override WebSocketCloseStatus? CloseStatus => _state == WebSocketState.Closed ? WebSocketCloseStatus.NormalClosure : null;
    public override string? CloseStatusDescription => _state == WebSocketState.Closed ? "Closed" : null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    /// <summary>Enqueue a text message that ReceiveAsync will return.</summary>
    public void EnqueueClientMessage(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        _inbound.Writer.TryWrite((bytes, WebSocketMessageType.Text));
    }

    /// <summary>Signal client disconnect so the read loop exits.</summary>
    public void EnqueueClose()
    {
        _inbound.Writer.TryWrite((Array.Empty<byte>(), WebSocketMessageType.Close));
    }

    /// <summary>All raw messages sent by the service to the client.</summary>
    public IReadOnlyCollection<(byte[] Data, WebSocketMessageType Type, bool EndOfMessage)> SentMessages => _sent;

    /// <summary>Convenience: decode text messages sent to client.</summary>
    public List<string> GetSentTextMessages()
    {
        var result = new List<string>();
        foreach (var (data, type, _) in _sent)
        {
            if (type == WebSocketMessageType.Text)
                result.Add(Encoding.UTF8.GetString(data));
        }
        return result;
    }

    public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return ReceiveInternalAsync(buffer, cancellationToken);
    }

    private Exception? _pendingException;

    /// <summary>Enqueue an exception that will be thrown on the next ReceiveAsync call.
    /// A dummy message is written to the channel to unblock any pending read.</summary>
    public void EnqueueError(Exception ex)
    {
        _pendingException = ex;
        _inbound.Writer.TryWrite((Array.Empty<byte>(), WebSocketMessageType.Text));
    }

    private async ValueTask<ValueWebSocketReceiveResult> ReceiveInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var (data, type) = await _inbound.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (_pendingException != null)
        {
            var ex = _pendingException;
            _pendingException = null;
            throw ex;
        }

        if (type == WebSocketMessageType.Close)
        {
            _state = WebSocketState.CloseReceived;
            return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        }

        data.AsSpan().CopyTo(buffer.Span);
        return new ValueWebSocketReceiveResult(data.Length, type, true);
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        // Delegate to the Memory-based overload which is what the SUT actually calls
        return ReceiveArraySegmentAsync(buffer, cancellationToken);
    }

    private async Task<WebSocketReceiveResult> ReceiveArraySegmentAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        var result = await ReceiveInternalAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        return new WebSocketReceiveResult(result.Count, result.MessageType, result.EndOfMessage);
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        _sent.Enqueue((buffer.ToArray(), messageType, endOfMessage));
        return Task.CompletedTask;
    }

    public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        _sent.Enqueue((buffer.ToArray(), messageType, endOfMessage));
        return ValueTask.CompletedTask;
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Abort()
    {
        _state = WebSocketState.Aborted;
    }

    public override void Dispose() { }
}
