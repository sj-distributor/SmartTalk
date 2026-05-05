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
        var written = _clientToServer.Writer.TryWrite((bytes, WebSocketMessageType.Text));
        Log.Debug("[DifyRealtimeWS:{TraceId}] Enqueue client text, TextLength: {TextLength}, QueueWriteSuccess: {QueueWriteSuccess}", _traceId, json?.Length ?? 0, written);
    }

    public void EnqueueClientClose()
    {
        var written = _clientToServer.Writer.TryWrite((Array.Empty<byte>(), WebSocketMessageType.Close));
        Log.Debug("[DifyRealtimeWS:{TraceId}] Enqueue client close, QueueWriteSuccess: {QueueWriteSuccess}", _traceId, written);
    }

    public async Task<string> ReadServerTextAsync(CancellationToken cancellationToken)
    {
        var text = await _serverToClient.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        Log.Debug("[DifyRealtimeWS:{TraceId}] Read server text, TextLength: {TextLength}", _traceId, text?.Length ?? 0);
        return text;
    }

    public int DrainServerMessages()
    {
        var count = 0;
        while (_serverToClient.Reader.TryRead(out _))
            count += 1;

        if (count > 0)
            Log.Debug("[DifyRealtimeWS:{TraceId}] Drained server messages, Count: {Count}", _traceId, count);

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
        Log.Debug("[DifyRealtimeWS:{TraceId}] ReceiveInternal dequeued, MessageType: {MessageType}, ByteCount: {ByteCount}", _traceId, type, data.Length);

        if (type == WebSocketMessageType.Close)
        {
            _state = WebSocketState.CloseReceived;
            Log.Debug("[DifyRealtimeWS:{TraceId}] Client close received, State -> {State}", _traceId, _state);
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
        Log.Debug("[DifyRealtimeWS:{TraceId}] Captured server message, TextLength: {TextLength}, MessageType: {MessageType}", _traceId, text.Length, messageType);
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
        Log.Debug("[DifyRealtimeWS:{TraceId}] CloseOutputAsync, State -> {State}, CloseStatus: {CloseStatus}, Description: {Description}", _traceId, _state, closeStatus, statusDescription);
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
        Log.Debug("[DifyRealtimeWS:{TraceId}] Disposed", _traceId);
    }
}
