using System.Net.WebSockets;
using System.Text;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;

namespace SmartTalk.Core.Services.RealtimeAiWebRtc;

public interface IOpenAiRealtimeWebRtcSidebandClient : IScopedDependency
{
    Task ConnectAsync(Uri endpoint, Dictionary<string, string> headers, CancellationToken cancellationToken);

    Task RunReceiveLoopAsync(Func<string, Task> onMessageAsync, CancellationToken cancellationToken);

    Task SendAsync(string message, CancellationToken cancellationToken);

    Task CloseAsync(string reason, CancellationToken cancellationToken);
}

public sealed class OpenAiRealtimeWebRtcSidebandClient : IOpenAiRealtimeWebRtcSidebandClient, IDisposable
{
    private const int ReceiveBufferSize = 16 * 1024;
    private const int MaxMessageSize = 2 * 1024 * 1024;

    private readonly ClientWebSocket _webSocket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public OpenAiRealtimeWebRtcSidebandClient()
    {
        _webSocket.Options.KeepAliveInterval = RealtimeAiWebSocketSettings.ResolveKeepAliveInterval();
    }

    public async Task ConnectAsync(
        Uri endpoint,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        foreach (var header in headers)
            _webSocket.Options.SetRequestHeader(header.Key, header.Value);

        await _webSocket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
        Log.Information("[RealtimeAiWebRtc] Sideband connected, Endpoint: {Endpoint}", endpoint.GetLeftPart(UriPartial.Path));
    }

    public async Task RunReceiveLoopAsync(Func<string, Task> onMessageAsync, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];

        while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var messageBuffer = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (_webSocket.State == WebSocketState.CloseReceived)
                    {
                        await _webSocket.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Acknowledging provider close",
                            CancellationToken.None).ConfigureAwait(false);
                    }

                    return;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                    throw new InvalidOperationException($"Unexpected sideband message type: {result.MessageType}.");

                messageBuffer.Write(buffer, 0, result.Count);
                if (messageBuffer.Length > MaxMessageSize)
                    throw new InvalidOperationException($"Sideband message exceeded {MaxMessageSize} bytes.");
            } while (!result.EndOfMessage);

            var message = Encoding.UTF8.GetString(messageBuffer.GetBuffer(), 0, checked((int)messageBuffer.Length));
            await onMessageAsync(message).ConfigureAwait(false);
        }
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        if (_webSocket.State != WebSocketState.Open)
            throw new InvalidOperationException($"Sideband WebSocket is not open (state: {_webSocket.State}).");

        var bytes = Encoding.UTF8.GetBytes(message);
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task CloseAsync(string reason, CancellationToken cancellationToken)
    {
        if (_webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived)) return;

        try
        {
            await _webSocket.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                reason,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _webSocket.Abort();
        }
        catch (WebSocketException ex)
        {
            Log.Warning(ex, "[RealtimeAiWebRtc] Failed to close sideband cleanly");
            _webSocket.Abort();
        }
    }

    public void Dispose()
    {
        _webSocket.Dispose();
        _sendLock.Dispose();
    }
}
