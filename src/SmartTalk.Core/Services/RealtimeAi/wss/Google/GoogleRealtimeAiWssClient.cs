using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;

namespace SmartTalk.Core.Services.RealtimeAi.wss.Google;

public class GoogleRealtimeAiWssClient : IRealtimeAiWssClient
{
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _receiveLoopCts;
    private Task _receiveLoopTask;

    public Uri EndpointUri { get; private set; }
    public WebSocketState CurrentState => _webSocket?.State ?? WebSocketState.None;

    public event Func<string, Task> MessageReceivedAsync;
    public event Func<Exception, Task> ErrorOccurredAsync;
    public event Func<WebSocketState, string, Task> StateChangedAsync;

    public async Task ConnectAsync(Uri endpointUri, Dictionary<string, string> customHeaders, CancellationToken cancellationToken)
    {
        if (_webSocket != null && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting))
        {
            // Notify error if already connected or connecting
            await OnErrorOccurredAsync(new InvalidOperationException("WebSocket is already connected or connecting.")).ConfigureAwait(false);
            return;
        }

        EndpointUri = endpointUri ?? throw new ArgumentNullException(nameof(endpointUri));
        _webSocket = new ClientWebSocket();

        // Set custom headers, e.g., API key or authentication token
        // Consult the "Live API" documentation for specific header requirements
        if (customHeaders != null)
        {
            foreach (var header in customHeaders)
            {
                if (!string.IsNullOrEmpty(header.Key) && header.Value != null)
                {
                    _webSocket.Options.SetRequestHeader(header.Key, header.Value);
                }
            }
        }
        // Add a specific WebSocket subprotocol if required by the "Live API"
        // _webSocket.Options.AddSubProtocol("your-api-specific-protocol");

        await OnStateChangedAsync(WebSocketState.Connecting, "Attempting to connect...").ConfigureAwait(false);

        try
        {
            await _webSocket.ConnectAsync(EndpointUri, cancellationToken).ConfigureAwait(false);
            await OnStateChangedAsync(_webSocket.State, "已连接").ConfigureAwait(false);

            var setupMessagePayload = new
            {
                model = "gemini-1.5-pro-latest",
            };
            string setupMessageJson = JsonConvert.SerializeObject(setupMessagePayload);

            await SendMessageAsync(setupMessageJson, cancellationToken).ConfigureAwait(false);
            await OnStateChangedAsync(_webSocket.State, "已发送 BidiGenerateContentSetup 消息").ConfigureAwait(false);

            _receiveLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_receiveLoopCts.Token));
        }
        catch (Exception ex)
        {
            await OnErrorOccurredAsync(ex).ConfigureAwait(false);
            await OnStateChangedAsync(_webSocket?.State ?? WebSocketState.Closed, $"连接失败: {ex.Message}").ConfigureAwait(false);
            DisposeWebSocket();
            throw;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<byte>(new byte[8192 * 2]);
        bool isSetupCompleteReceived = false; // Flag to indicate if SetupComplete message has been received

        try
        {
            while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using (var ms = new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            await OnStateChangedAsync(_webSocket.State, "Receive loop cancellation requested during message read.").ConfigureAwait(false);
                            return;
                        }

                        result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await OnStateChangedAsync(WebSocketState.CloseReceived,
                                result.CloseStatusDescription ?? "Server closed the connection").ConfigureAwait(false);
                            if (_webSocket.State == WebSocketState.CloseReceived &&
                                _webSocket.CloseStatus.HasValue &&
                                !cancellationToken.IsCancellationRequested)
                            {
                                await _webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription,
                                    CancellationToken.None).ConfigureAwait(false);
                                await OnStateChangedAsync(_webSocket.State, "Server closure acknowledged").ConfigureAwait(false);
                            }

                            return;
                        }

                        if (buffer.Array != null)
                        {
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                    } while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(ms.ToArray());

                        // Check if it is the BidiGenerateContentSetupComplete message (empty JSON or empty string)
                        if (string.IsNullOrWhiteSpace(message) || message == "{}")
                        {
                            isSetupCompleteReceived = true;
                            await OnStateChangedAsync(_webSocket.State, "Received BidiGenerateContentSetupComplete message, session established.")
                                .ConfigureAwait(false);
                            // You can trigger an event or set a state here to notify that the session is established
                            continue; // Continue receiving subsequent messages
                        }

                        // If it's not the SetupComplete message, pass it to the MessageReceivedAsync event handler
                        await OnMessageReceivedAsync(message).ConfigureAwait(false);
                    }
                    // You can add handling for Binary message type if the Gemini API sends it
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely ||
                                            ex.InnerException is ObjectDisposedException)
        {
            await OnStateChangedAsync(_webSocket?.State ?? WebSocketState.Aborted, $"Receive loop ended: {ex.Message}")
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await OnStateChangedAsync(_webSocket?.State ?? WebSocketState.Aborted, "Receive loop cancelled.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await OnErrorOccurredAsync(ex).ConfigureAwait(false);
            await OnStateChangedAsync(_webSocket?.State ?? WebSocketState.Aborted, $"Receive loop error: {ex.Message}")
                .ConfigureAwait(false);
        }
        finally
        {
            if (_webSocket != null && _webSocket.State != WebSocketState.Closed &&
                _webSocket.State != WebSocketState.Aborted)
            {
                if (cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
                {
                    await OnStateChangedAsync(WebSocketState.Aborted, "Receive loop stopped while connection was open due to cancellation.").ConfigureAwait(false);
                }
                else
                {
                    await OnStateChangedAsync(_webSocket.State, "Receive loop finished.").ConfigureAwait(false);
                }
            }
        }
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            var ex = new InvalidOperationException("WebSocket is not connected.");
            await OnErrorOccurredAsync(ex).ConfigureAwait(false);
            throw ex;
        }

        if (string.IsNullOrEmpty(message))
        {
            var ex = new ArgumentNullException(nameof(message));
            await OnErrorOccurredAsync(ex).ConfigureAwait(false);
            throw ex;
        }

        // Ensure the `message` is a string formatted according to the "Live API" specification (usually JSON)
        var messageBuffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(messageBuffer);

        try
        {
            // Send the text message to the "Live API"
            await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await OnErrorOccurredAsync(ex).ConfigureAwait(false);
            await OnStateChangedAsync(_webSocket.State, $"Failed to send message: {ex.Message}").ConfigureAwait(false);
            throw;
        }
    }

    public async Task DisconnectAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        if (_webSocket == null || _webSocket.State == WebSocketState.None || _webSocket.State == WebSocketState.Closed)
        {
            await OnStateChangedAsync(_webSocket?.State ?? WebSocketState.Closed, "Already disconnected or not connected.").ConfigureAwait(false);
            return;
        }

        // Notify the receive loop to stop
        _receiveLoopCts?.Cancel();

        try
        {
            if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
            {
                await OnStateChangedAsync(WebSocketState.CloseSent, statusDescription ?? "Client initiated disconnection").ConfigureAwait(false);
                // Initiate closure of the connection with the "Live API"
                await _webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken).ConfigureAwait(false);
            }
            else if (_webSocket.State == WebSocketState.Connecting)
            {
                 // Abort the connection attempt if still connecting
                _webSocket.Abort();
                await OnStateChangedAsync(WebSocketState.Aborted, "Connection attempt aborted during disconnection.").ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex)
        {
            _webSocket?.Abort(); // Ensure the socket enters the aborted state
            await OnErrorOccurredAsync(ex).ConfigureAwait(false);
            await OnStateChangedAsync(_webSocket?.State ?? WebSocketState.Aborted, $"Disconnection operation cancelled: {ex.Message}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _webSocket?.Abort(); // Abort on failure to ensure aborted state
            await OnErrorOccurredAsync(ex).ConfigureAwait(false);
            await OnStateChangedAsync(_webSocket?.State ?? WebSocketState.Aborted, $"Disconnection failed: {ex.Message}").ConfigureAwait(false);
        }
        finally
        {
            // Wait for the receive loop task to complete
            if (_receiveLoopTask != null && !_receiveLoopTask.IsCompleted)
            {
                try
                {
                    // Give the receive loop a moment to close gracefully after cancellation
                    await Task.WhenAny(_receiveLoopTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)).ConfigureAwait(false);
                    if (!_receiveLoopTask.IsCompleted)
                    {
                        await OnErrorOccurredAsync(new TimeoutException("Receive loop did not complete in time during disconnection.")).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { /* Expected if the external token is cancelled */ }
                catch (Exception ex)
                {
                    await OnErrorOccurredAsync(ex).ConfigureAwait(false); // Log errors occurring while waiting for the receive loop
                }
            }
            DisposeWebSocket(); // Clean up WebSocket resources
            await OnStateChangedAsync(WebSocketState.Closed, statusDescription ?? "Disconnected").ConfigureAwait(false);
        }
    }
    
    private void DisposeWebSocket()
    {
        _receiveLoopCts?.Cancel(); // Ensure cancellation
        _webSocket?.Dispose();
        _webSocket = null;
        _receiveLoopCts?.Dispose();
        _receiveLoopCts = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_webSocket != null)
        {
            // Use a new CancellationToken for dispose, as the original might be released or cancelled
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await DisconnectAsync(WebSocketCloseStatus.NormalClosure, "Client disposed", cts.Token).ConfigureAwait(false);
        }
        DisposeWebSocket(); // Ensure resource disposal even if DisconnectAsync has issues or is not needed
        
        GC.SuppressFinalize(this); // Prevent the garbage collector from calling the finalizer of this object
        await OnStateChangedAsync(WebSocketState.Closed, "Disposed").ConfigureAwait(false);
    }

    // Helper method to safely invoke the MessageReceivedAsync event
    private async Task OnMessageReceivedAsync(string message)
    {
        if (MessageReceivedAsync != null)
        {
            try
            {
                await MessageReceivedAsync.Invoke(message).ConfigureAwait(false);
            }
            catch(Exception handlerEx)
            {
                // Consider reporting this internal handler error via ErrorOccurredAsync as well
                 _ = OnErrorOccurredAsync(new Exception("Exception occurred in MessageReceivedAsync handler.", handlerEx));
            }
        }
    }

    private async Task OnErrorOccurredAsync(Exception exception)
    {
        if (ErrorOccurredAsync != null)
        {
            await ErrorOccurredAsync.Invoke(exception).ConfigureAwait(false);
            
        }
    }

    private async Task OnStateChangedAsync(WebSocketState state, string reason)
    {
        if (StateChangedAsync != null)
        {
             try
             {
                 await StateChangedAsync.Invoke(state, reason).ConfigureAwait(false);
             }
             catch(Exception handlerEx)
             {
                 _ = OnErrorOccurredAsync(new Exception($"Exception occurred in StateChangedAsync handler (State: {state}, Reason: {reason}).", handlerEx));
             }
        }
    }
}