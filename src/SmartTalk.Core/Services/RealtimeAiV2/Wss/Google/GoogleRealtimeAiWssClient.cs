using System.Net.WebSockets;
using System.Text;
using Serilog;
using SmartTalk.Core.Settings.Google;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.RealtimeAiV2.Wss.Google;

public class GoogleRealtimeAiWssClient : IRealtimeAiWssClient
{
    private Task _receiveLoopTask;
    private ClientWebSocket _webSocket;
    private readonly GoogleSettings _googleSettings;

    public Uri EndpointUri { get; private set; }
    public AiSpeechAssistantProvider Provider => AiSpeechAssistantProvider.Google;
    public WebSocketState CurrentState => _webSocket?.State ?? WebSocketState.None;

    public event Func<string, Task> MessageReceivedAsync;
    public event Func<Exception, Task> ErrorOccurredAsync;
    public event Func<WebSocketState, string, Task> StateChangedAsync;

    public GoogleRealtimeAiWssClient(GoogleSettings googleSettings)
    {
        _googleSettings = googleSettings;
        _webSocket = new ClientWebSocket();
    }

    public async Task ConnectAsync(Uri endpointUri, Dictionary<string, string> customHeaders, CancellationToken cancellationToken)
    {
        EndpointUri = new Uri($"{endpointUri}?key={_googleSettings.ApiKey}");

        try
        {
            Log.Information("Google Realtime Wss Client: Connecting to {EndpointUri}...", EndpointUri);
            await _webSocket.ConnectAsync(EndpointUri, cancellationToken).ConfigureAwait(false);
            Log.Information("Google Realtime Wss Client: Successfully connected to {EndpointUri}. State: {State}", EndpointUri, CurrentState);
            await (StateChangedAsync?.Invoke(CurrentState, "Connected") ?? Task.CompletedTask);

            _receiveLoopTask = Task.Run(() => ReceiveMessagesAsync(cancellationToken), cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Google Realtime Wss Client: Failed to connect to {EndpointUri}.", EndpointUri);
            await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask);
            await (StateChangedAsync?.Invoke(_webSocket?.State ?? WebSocketState.Closed, $"Google Realtime Wss Connection failed: {ex.Message}") ?? Task.CompletedTask);
            await CleanUpCurrentConnectionAsync("Google Realtime Wss Connection failed."); // Ensure resources are cleaned up on failure
            throw;
        }
    }

    private async Task ReceiveMessagesAsync(CancellationToken token)
    {
        var buffer = new ArraySegment<byte>(new byte[8192]); // 8KB buffer
        try
        {
            while (CurrentState == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                using var ms = new MemoryStream();
                do
                {
                    result = await _webSocket.ReceiveAsync(buffer, token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log.Information("RealtimeClient: WebSocket close message received. Status: {Status}, Description: {Description}", result.CloseStatus, result.CloseStatusDescription);
                        // If initiated by a server, we should try to close from our side too if state allows.
                        if (CurrentState == WebSocketState.CloseReceived) {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledging server close", CancellationToken.None);
                        }
                        await (StateChangedAsync?.Invoke(WebSocketState.Closed, $"Closed by remote: {result.CloseStatusDescription}") ?? Task.CompletedTask);
                        return; // Exit loop
                    }

                    if (buffer.Array != null) ms.Write(buffer.Array, buffer.Offset, result.Count);
                } while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                var message = Encoding.UTF8.GetString(ms.ToArray());
                Log.Information("RealtimeClient: Message received from {EndpointUri}: {Message}", EndpointUri, message);
                await (MessageReceivedAsync?.Invoke(message) ?? Task.CompletedTask);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Log.Information("RealtimeClient: Receive loop for {EndpointUri} was canceled.", EndpointUri);
            await (StateChangedAsync?.Invoke(CurrentState, "Receive loop canceled by client") ?? Task.CompletedTask);
        }
        catch (WebSocketException ex)
        {
            Log.Error(ex, "RealtimeClient: WebSocketException in receive loop for {EndpointUri}.", EndpointUri);
            await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask);
            await (StateChangedAsync?.Invoke(CurrentState, $"WebSocketException: {ex.Message}") ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "RealtimeClient: Unexpected error in receive loop for {EndpointUri}.", EndpointUri);
            await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask);
            await (StateChangedAsync?.Invoke(CurrentState, $"Unexpected error: {ex.Message}") ?? Task.CompletedTask);
        }
        finally
        {
             Log.Debug("RealtimeClient: Receive loop for {EndpointUri} ended. Final WebSocket state: {State}", EndpointUri, CurrentState);
             // If the loop exits and the socket is not in a closed state, it might indicate an issue.
             if (CurrentState != WebSocketState.Closed && CurrentState != WebSocketState.Aborted)
             {
                 // This might happen if cancellation occurred but the socket didn't fully close yet
                 // Or if an error occurred that didn't change the state to Closed/Aborted
                 Log.Warning("RealtimeClient: Receive loop ended for {EndpointUri} but socket state is {State}.", EndpointUri, CurrentState);
             }
        }
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        if (CurrentState != WebSocketState.Open)
        {
            var ex = new InvalidOperationException($"RealtimeClient: WebSocket is not open for sending. Current state: {CurrentState}");
            Log.Error(ex, "RealtimeClient: Failed to send message to {EndpointUri}.", EndpointUri);
            await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask);
            throw ex;
        }
        Log.Information("准备发送信息: {@Message}", message);
        // Log.Verbose("RealtimeClient: Sending message to {EndpointUri}: {Message}", EndpointUri, message);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task DisconnectAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        if (_webSocket == null)
        {
            Log.Information("RealtimeClient: Disconnect called but WebSocket is null (not connected or already disposed).");
            return;
        }
        Log.Information("RealtimeClient: Disconnecting from {EndpointUri}. Reason: {Reason}", EndpointUri, statusDescription);

        if (CurrentState is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                // Give a short timeout for the server to acknowledge the close.
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                await _webSocket.CloseOutputAsync(closeStatus, statusDescription, cts.Token);
                Log.Information("RealtimeClient: CloseOutputAsync completed for {EndpointUri}.", EndpointUri);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                 Log.Warning("RealtimeClient: DisconnectAsync was cancelled by caller token during CloseOutputAsync for {EndpointUri}.", EndpointUri);
            }
            catch (OperationCanceledException ex) { // Timeout from cts.CancelAfter
                 Log.Warning(ex, "RealtimeClient: Timeout or cancellation during CloseOutputAsync for {EndpointUri}.", EndpointUri);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RealtimeClient: Error during WebSocket CloseOutputAsync for {EndpointUri}.", EndpointUri);
                // Don't rethrow, proceed to cleanup
            }
        } else {
            Log.Information("RealtimeClient: WebSocket for {EndpointUri} not in Open/CloseReceived state ({State}), skipping CloseOutputAsync.", EndpointUri, CurrentState);
        }

        // Wait for the receive loop to finish, if it was running
        if (_receiveLoopTask is { IsCompleted: false })
        {
            try
            {
                await Task.WhenAny(_receiveLoopTask, Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None)); // Brief wait
                if (!_receiveLoopTask.IsCompleted)
                {
                     Log.Warning("RealtimeClient: Receive loop for {EndpointUri} did not complete after DisconnectAsync.", EndpointUri);
                }
            } catch (Exception ex) {
                Log.Warning(ex, "RealtimeClient: Exception while waiting for receive loop to complete during disconnect of {EndpointUri}.", EndpointUri);
            }
        }
        
        await (StateChangedAsync?.Invoke(CurrentState, statusDescription) ?? Task.CompletedTask);
        // Final cleanup is handled by DisposeAsync or when a new connection is made.
    }
    
    private async Task CleanUpCurrentConnectionAsync(string reason)
    {
        Log.Debug("RealtimeClient: Cleaning up current connection. Reason: {Reason}", reason);
        
        if (_receiveLoopTask is { IsCompleted: false })
        {
            Log.Debug("RealtimeClient: Waiting for previous receive loop to complete during cleanup.");
            try { await Task.WhenAny(_receiveLoopTask, Task.Delay(1000)); } catch { /* ignore */ } // Best effort
        }
        _receiveLoopTask = null;

        if (_webSocket != null)
        {
            if (CurrentState == WebSocketState.Open || CurrentState == WebSocketState.CloseReceived)
            {
                try { await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, $"Cleaning up: {reason}", CancellationToken.None); } catch { /* ignore */ }
            }
            _webSocket.Dispose();
        }
        _webSocket = null; // Ensure it's null so a new one is created on next ConnectAsync
        
        Log.Debug("RealtimeClient: Cleanup complete.");
    }

    public async ValueTask DisposeAsync()
    {
        Log.Information("RealtimeClient: Disposing client for {EndpointUri}.", EndpointUri);
        await DisconnectAsync(WebSocketCloseStatus.NormalClosure, "Client disposing", CancellationToken.None);
        await CleanUpCurrentConnectionAsync("Disposing client."); // Redundant but safe
        GC.SuppressFinalize(this);
    }
}