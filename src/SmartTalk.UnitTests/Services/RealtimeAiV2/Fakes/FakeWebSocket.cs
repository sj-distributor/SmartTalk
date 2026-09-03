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
    private readonly Channel<(byte[] Data, WebSocketMessageType Type, bool EndOfMessage)> _inbound = Channel.CreateUnbounded<(byte[], WebSocketMessageType, bool)>();
    private readonly ConcurrentQueue<(byte[] Data, WebSocketMessageType Type, bool EndOfMessage)> _sent = new();

    public override WebSocketCloseStatus? CloseStatus => _state == WebSocketState.Closed ? WebSocketCloseStatus.NormalClosure : null;
    public override string? CloseStatusDescription => _state == WebSocketState.Closed ? "Closed" : null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    /// <summary>Enqueue a text message that ReceiveAsync will return.</summary>
    public void EnqueueClientMessage(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        _inbound.Writer.TryWrite((bytes, WebSocketMessageType.Text, true));
    }

    /// <summary>
    /// Enqueue a text message delivered as several WebSocket continuation frames, the way a real
    /// client sends anything past its fragment size. The plain enqueue always sets EndOfMessage, so
    /// the reassembly branch of the read loop had no coverage at all before this existed.
    /// </summary>
    public void EnqueueFragmentedClientMessage(string json, int chunkSize)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        for (var offset = 0; offset < bytes.Length; offset += chunkSize)
        {
            var count = Math.Min(chunkSize, bytes.Length - offset);
            var isLast = offset + count >= bytes.Length;

            _inbound.Writer.TryWrite((bytes[offset..(offset + count)], WebSocketMessageType.Text, isLast));
        }
    }

    /// <summary>Enqueue a partial frame that is never completed, so a close can arrive mid-message.</summary>
    public void EnqueuePartialClientMessage(string partialJson)
    {
        _inbound.Writer.TryWrite((Encoding.UTF8.GetBytes(partialJson), WebSocketMessageType.Text, false));
    }

    /// <summary>Signal client disconnect so the read loop exits.</summary>
    public void EnqueueClose()
    {
        _inbound.Writer.TryWrite((Array.Empty<byte>(), WebSocketMessageType.Close, true));
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

    private readonly TaskCompletionSource _receiveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes when the service's read loop first calls ReceiveAsync — i.e. the session is fully
    /// up. Tests wait on this instead of guessing with a fixed delay, which loses the race whenever
    /// the machine is busy running other test classes in parallel.
    /// </summary>
    public Task ReceiveStarted => _receiveStarted.Task;

    public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        _receiveStarted.TrySetResult();

        return ReceiveInternalAsync(buffer, cancellationToken);
    }

    private Exception? _pendingException;

    /// <summary>Enqueue an exception that will be thrown on the next ReceiveAsync call.
    /// A dummy message is written to the channel to unblock any pending read.</summary>
    public void EnqueueError(Exception ex)
    {
        _pendingException = ex;
        _inbound.Writer.TryWrite((Array.Empty<byte>(), WebSocketMessageType.Text, true));
    }

    private async ValueTask<ValueWebSocketReceiveResult> ReceiveInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var (data, type, endOfMessage) = await _inbound.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

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
        return new ValueWebSocketReceiveResult(data.Length, type, endOfMessage);
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
