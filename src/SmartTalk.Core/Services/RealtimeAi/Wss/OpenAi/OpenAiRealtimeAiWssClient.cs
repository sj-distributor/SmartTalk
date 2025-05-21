using Serilog;
using System.Text;
using Websocket.Client;
using System.Reactive.Linq;
using System.Net.WebSockets;
using System.Reactive.Concurrency;
using Newtonsoft.Json;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAi.Adapters;

namespace SmartTalk.Core.Services.RealtimeAi.wss.OpenAi;

public class OpenAiRealtimeAiWssClient : IRealtimeAiWssClient
{
    private string _connectionId;
    private IWebsocketClient _websocketClient;

    public OpenAiRealtimeAiWssClient(IWebsocketClient websocketClient)
    {
        _websocketClient = websocketClient;
    }

    public Uri EndpointUri { get; private set; }
    public AiSpeechAssistantProvider Provider => AiSpeechAssistantProvider.OpenAi;
    public WebSocketState CurrentState => _websocketClient?.IsRunning == true ? _websocketClient.NativeClient?.State ?? WebSocketState.None : WebSocketState.Closed;

    public event Func<string, Task> MessageReceivedAsync;
    public event Func<Exception, Task> ErrorOccurredAsync;
    public event Func<WebSocketState, string, Task> StateChangedAsync;

    public async Task ConnectAsync(Uri endpointUri, Dictionary<string, string> customHeaders, CancellationToken cancellationToken)
    {
        EndpointUri = endpointUri;
        _connectionId = Guid.NewGuid().ToString("N");
        
        await CleanUpCurrentConnectionAsync("Preparing for new connection.");
        _websocketClient = GetClient(customHeaders).WithReconnect(endpointUri.ToString());

        RegisterWebsocketEvents();

        try
        {
            Log.Information("Connecting to {EndpointUri} (ConnectionId: {ConnectionId})", EndpointUri, _connectionId);

            await _websocketClient.Start().ConfigureAwait(false);

            Log.Information("Connected to {EndpointUri}. State: {State}", EndpointUri, _websocketClient.NativeClient?.State);

            await (StateChangedAsync?.Invoke(_websocketClient?.NativeClient?.State ?? WebSocketState.Open, "Connected") ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect to {EndpointUri} (ConnectionId: {ConnectionId})", EndpointUri, _connectionId);
            await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask);
            await (StateChangedAsync?.Invoke(_websocketClient?.NativeClient?.State ?? WebSocketState.Closed, $"Connection failed: {ex.Message}") ?? Task.CompletedTask);
            await CleanUpCurrentConnectionAsync("Connection failed.");
            throw;
        }
    }
    
    private ClientWebSocket GetClient(Dictionary<string, string> customHeaders)
    {
        var websocket = new ClientWebSocket
        {
            Options = { KeepAliveInterval = TimeSpan.FromSeconds(10) }
        };
        
        if (customHeaders != null)
        {
            foreach (var header in customHeaders)
            {
                websocket.Options.SetRequestHeader(header.Key, header.Value);
            }
        }

        return websocket;
    }

    private void RegisterWebsocketEvents()
    {
        _websocketClient.ReconnectionHappened.Subscribe(async info =>
        {
            Log.Information("Reconnection happened: {Type} (ConnectionId: {ConnectionId})", info.Type, _connectionId);
            await (StateChangedAsync?.Invoke(_websocketClient.NativeClient.State, "Reconnected") ?? Task.CompletedTask);
        });

        _websocketClient.MessageReceived.ObserveOn(TaskPoolScheduler.Default).Subscribe(async msg =>
        {
            try
            {
                var message = msg.Text ?? (msg.Binary != null ? Encoding.UTF8.GetString(msg.Binary) : null);

                if (!string.IsNullOrWhiteSpace(message))
                {
                    await (MessageReceivedAsync?.Invoke(message) ?? Task.CompletedTask);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling message (ConnectionId: {ConnectionId})", _connectionId);
                await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask);
            }
        });

        _websocketClient.DisconnectionHappened.Subscribe(async info =>
        {
            if (info.Type == DisconnectionType.Error)
            {
                Log.Error("Abnormal disconnect: {@Info} (ConnectionId: {ConnectionId})", info, _connectionId);
                await (ErrorOccurredAsync?.Invoke(info.Exception) ?? Task.CompletedTask);
            }
            else
            {
                Log.Warning("Disconnected cleanly: {@Info} (ConnectionId: {ConnectionId})", info, _connectionId);
            }

            await CleanUpCurrentConnectionAsync("Disconnected").ConfigureAwait(false);
        });
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        if (_websocketClient?.IsRunning != true)
        {
            var ex = new InvalidOperationException("The WebSocket client is not connected.");
            Log.Error(ex, "SendMessageAsync called when not connected (ConnectionId: {ConnectionId})", _connectionId);
            await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask);
            throw ex;
        }

        try
        {
            await _websocketClient?.NativeClient.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message))), WebSocketMessageType.Text, true, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending message (ConnectionId: {ConnectionId})", _connectionId);
            await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask);
            throw;
        }
    }

    public async Task DisconnectAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        if (_websocketClient != null)
        {
            try
            {
                Log.Information("Disconnecting from {EndpointUri} (ConnectionId: {ConnectionId})", EndpointUri, _connectionId);
                await _websocketClient.Stop(closeStatus, statusDescription).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during disconnect (ConnectionId: {ConnectionId})", _connectionId);
                await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask);
            }
            finally
            {
                _websocketClient.Dispose();
                _websocketClient = null;
            }
        }
    }
    
    private async Task CleanUpCurrentConnectionAsync(string reason)
    {
        if (_websocketClient != null)
        {
            Log.Debug("Cleaning up WebSocket connection (ConnectionId: {ConnectionId}) Reason: {Reason}", _connectionId, reason);
            await DisconnectAsync(WebSocketCloseStatus.NormalClosure, "Cleanup", CancellationToken.None);
            Log.Debug("Cleanup complete (ConnectionId: {ConnectionId})", _connectionId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Log.Information("Disposing client (ConnectionId: {ConnectionId})", _connectionId);
        await CleanUpCurrentConnectionAsync("Disposing client");
        GC.SuppressFinalize(this);
    }
}