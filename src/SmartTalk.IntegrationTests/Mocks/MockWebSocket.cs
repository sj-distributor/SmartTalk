using System.Net.WebSockets;
using System.Text;

namespace SmartTalk.IntegrationTests.Mocks;

public class MockWebSocket : WebSocket
{
    private readonly Queue<string> _messageQueue = new();
    private readonly bool _waitForCloseSignal;
    private volatile WebSocketState _state = WebSocketState.Open;
    private WebSocketCloseStatus? _closeStatus;
    private string _closeStatusDescription;
    private readonly TaskCompletionSource<bool> _closeSignal = new();

    public List<byte[]> SentMessages { get; } = new();

    public override WebSocketCloseStatus? CloseStatus => _closeStatus;
    public override string CloseStatusDescription => _closeStatusDescription;
    public override WebSocketState State => _state;
    public override string SubProtocol => null;

    public MockWebSocket(bool waitForCloseSignal = false)
    {
        _waitForCloseSignal = waitForCloseSignal;
    }

    public void EnqueueMessage(string json)
    {
        _messageQueue.Enqueue(json);
    }

    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        if (_messageQueue.Count > 0)
        {
            var message = _messageQueue.Dequeue();
            var bytes = Encoding.UTF8.GetBytes(message);
            Array.Copy(bytes, 0, buffer.Array!, buffer.Offset, bytes.Length);
            return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true);
        }

        if (_waitForCloseSignal)
            await _closeSignal.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        _state = WebSocketState.CloseReceived;
        return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        SentMessages.Add(buffer.ToArray());
        return Task.CompletedTask;
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        _closeStatus = closeStatus;
        _closeStatusDescription = statusDescription;

        if (_waitForCloseSignal)
            _closeSignal.TrySetResult(true);
        else
            _state = WebSocketState.Closed;

        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.CloseSent;
        _closeStatus = closeStatus;
        _closeStatusDescription = statusDescription;
        return Task.CompletedTask;
    }

    public override void Abort()
    {
        _state = WebSocketState.Aborted;
    }

    public override void Dispose()
    {
    }
}
