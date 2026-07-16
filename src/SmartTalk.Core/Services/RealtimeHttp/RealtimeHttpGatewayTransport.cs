using System.Net.WebSockets;
using System.Text;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.RealtimeHttp;

public interface IRealtimeHttpGatewayTransport : IAsyncDisposable
{
    WebSocketState State { get; }

    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    Task SendTextAsync(string rawMessage, CancellationToken cancellationToken);

    Task<string> ReceiveTextAsync(CancellationToken cancellationToken);

    Task CloseAsync(string reason, CancellationToken cancellationToken);
}

public interface IRealtimeHttpGatewayTransportFactory : ISingletonDependency
{
    IRealtimeHttpGatewayTransport Create();
}

public class RealtimeHttpGatewayTransportFactory : IRealtimeHttpGatewayTransportFactory
{
    public IRealtimeHttpGatewayTransport Create()
    {
        return new ClientWebSocketRealtimeHttpGatewayTransport();
    }
}

public sealed class ClientWebSocketRealtimeHttpGatewayTransport : IRealtimeHttpGatewayTransport
{
    private readonly ClientWebSocket _socket = new();

    public WebSocketState State => _socket.State;

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        return _socket.ConnectAsync(uri, cancellationToken);
    }

    public async Task SendTextAsync(string rawMessage, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawMessage);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ReceiveTextAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();

        while (true)
        {
            var receive = await _socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (receive.MessageType == WebSocketMessageType.Close)
                return null;

            ms.Write(buffer, 0, receive.Count);
            if (receive.EndOfMessage)
                return Encoding.UTF8.GetString(ms.ToArray());
        }
    }

    public async Task CloseAsync(string reason, CancellationToken cancellationToken)
    {
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, reason, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _socket.Dispose();
        return ValueTask.CompletedTask;
    }
}
