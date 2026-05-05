using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Serilog;

namespace SmartTalk.Core.Services.DifyRealtime;

public class DifyRealtimeWebSocket : WebSocket
{
    private readonly string _traceId = Guid.NewGuid().ToString("N")[..8];
    private WebSocketState _state = WebSocketState.Open;
    private readonly Channel<(byte[] Data, WebSocketMessageType Type)> _clientToServer = Channel.CreateUnbounded<(byte[], WebSocketMessageType)>();
    private readonly Channel<string> _serverToClient = Channel.CreateUnbounded<string>();
    private readonly ConcurrentQueue<string> _sentMessages = new();

    public override WebSocketCloseStatus? CloseStatus => _state == WebSocketState.Closed ? WebSocketCloseStatus.NormalClosure : null;

    public override string CloseStatusDescription => _state == WebSocketState.Closed ? "Closed" : null;

    public override WebSocketState State => _state;

    public override string SubProtocol => null;

    public void EnqueueClientText(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        _clientToServer.Writer.TryWrite((bytes, WebSocketMessageType.Text));
    }

    public void EnqueueClientClose()
    {
        _clientToServer.Writer.TryWrite((Array.Empty<byte>(), WebSocketMessageType.Close));
    }

    public async Task<string> ReadServerTextAsync(CancellationToken cancellationToken)
    {
        return await _serverToClient.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    public int DrainServerMessages()
    {
        var count = 0;
        while (_serverToClient.Reader.TryRead(out _))
            count += 1;

        return count;
    }

    public IReadOnlyCollection<string> SentMessages => _sentMessages;

    public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return ReceiveInternalAsync(buffer, cancellationToken);
    }

    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        var result = await ReceiveInternalAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        return new WebSocketReceiveResult(result.Count, result.MessageType, result.EndOfMessage);
    }

    private async ValueTask<ValueWebSocketReceiveResult> ReceiveInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var (data, type) = await _clientToServer.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (type == WebSocketMessageType.Close)
        {
            _state = WebSocketState.CloseReceived;
            return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        }

        data.AsSpan().CopyTo(buffer.Span);
        return new ValueWebSocketReceiveResult(data.Length, type, true);
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        CaptureServerMessage(buffer.ToArray(), messageType);
        return Task.CompletedTask;
    }

    public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        CaptureServerMessage(buffer.ToArray(), messageType);
        return ValueTask.CompletedTask;
    }

    private void CaptureServerMessage(byte[] data, WebSocketMessageType messageType)
    {
        if (messageType != WebSocketMessageType.Text) return;

        var text = Encoding.UTF8.GetString(data);
        _sentMessages.Enqueue(text);
        _serverToClient.Writer.TryWrite(text);
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        _serverToClient.Writer.TryComplete();
        _clientToServer.Writer.TryComplete();
        Log.Information("[DifyRealtimeWS:{TraceId}] CloseAsync, State -> {State}, CloseStatus: {CloseStatus}, Description: {Description}", _traceId, _state, closeStatus, statusDescription);
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Abort()
    {
        _state = WebSocketState.Aborted;
        _serverToClient.Writer.TryComplete();
        _clientToServer.Writer.TryComplete();
        Log.Warning("[DifyRealtimeWS:{TraceId}] Abort called, State -> {State}", _traceId, _state);
    }

    public override void Dispose()
    {
        _serverToClient.Writer.TryComplete();
        _clientToServer.Writer.TryComplete();
    }
}
