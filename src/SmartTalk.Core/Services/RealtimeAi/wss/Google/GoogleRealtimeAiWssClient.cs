using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Serilog;

namespace SmartTalk.Core.Services.RealtimeAi.wss.Google
{
    public class GoogleRealtimeAiWssClient : IRealtimeAiWssClient
    {
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _receiveLoopCts;
        private Task _receiveLoopTask;
        private readonly object _lock = new();

        public Uri EndpointUri { get; private set; }
        public WebSocketState CurrentState => _webSocket?.State ?? WebSocketState.None;

        public event Func<string, Task> MessageReceivedAsync;
        public event Func<Exception, Task> ErrorOccurredAsync;
        public event Func<WebSocketState, string, Task> StateChangedAsync;

        public async Task ConnectAsync(Uri endpointUri, Dictionary<string, string> customHeaders, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_webSocket != null && _webSocket.State != WebSocketState.Closed && _webSocket.State != WebSocketState.Aborted && _webSocket.State != WebSocketState.None)
                {
                    Log.Warning("Google Realtime Wss Client: Attempting to connect while already in state {State}. Consider disconnecting first.", _webSocket.State);
                }
            }

            await CleanUpCurrentConnectionAsync("Preparing for new connection.");

            EndpointUri = endpointUri ?? throw new ArgumentNullException(nameof(endpointUri));
            _webSocket = new ClientWebSocket();

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

            _receiveLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                Log.Information("Google Realtime Wss Client: Connecting to {EndpointUri}...", EndpointUri);
                await _webSocket.ConnectAsync(EndpointUri, _receiveLoopCts.Token).ConfigureAwait(false);
                Log.Information("Google Realtime Wss Client: Successfully connected to {EndpointUri}. State: {State}", EndpointUri, _webSocket.State);
                await (StateChangedAsync?.Invoke(_webSocket.State, "已连接") ?? Task.CompletedTask).ConfigureAwait(false);

                // 发送 Gemini 特定的设置消息
                var setupMessagePayload = new
                {
                    model = "gemini-1.5-pro-latest", // 根据实际 Gemini API 要求设置
                    // 可以添加其他 Gemini 特有的配置参数
                };
                string setupMessageJson = JsonConvert.SerializeObject(setupMessagePayload);

                await SendMessageAsync(setupMessageJson, cancellationToken).ConfigureAwait(false);
                await (StateChangedAsync?.Invoke(_webSocket.State, "已发送 Gemini BidiGenerateContentSetup 消息") ?? Task.CompletedTask).ConfigureAwait(false);

                _receiveLoopTask = Task.Run(() => ReceiveMessagesAsync(_receiveLoopCts.Token), cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Google Realtime Wss Client: Failed to connect to {EndpointUri}.", EndpointUri);
                await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask).ConfigureAwait(false);
                await (StateChangedAsync?.Invoke(_webSocket?.State ?? WebSocketState.Closed, $"连接失败: {ex.Message}") ?? Task.CompletedTask).ConfigureAwait(false);
                await CleanUpCurrentConnectionAsync("Google Realtime Wss Connection failed.");
                throw;
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken token)
        {
            var buffer = new ArraySegment<byte>(new byte[8192 * 2]);
            bool isSetupCompleteReceived = false; // Flag to indicate if SetupComplete message has been received

            try
            {
                while (_webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    using var ms = new MemoryStream();
                    do
                    {
                        result = await _webSocket.ReceiveAsync(buffer, token).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Log.Information("Google Realtime Client: WebSocket close message received. Status: {Status}, Description: {Description}", result.CloseStatus, result.CloseStatusDescription);
                            if (_webSocket.State == WebSocketState.CloseReceived && _webSocket.CloseStatus.HasValue)
                            {
                                await _webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None).ConfigureAwait(false);
                            }
                            await (StateChangedAsync?.Invoke(WebSocketState.Closed, $"Closed by remote: {result.CloseStatusDescription}") ?? Task.CompletedTask).ConfigureAwait(false);
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

                        // 检查是否是 Gemini 特定的 SetupComplete 消息 (根据 Gemini API 文档调整)
                        if (string.IsNullOrWhiteSpace(message) || message == "{}")
                        {
                            isSetupCompleteReceived = true;
                            Log.Information("Google Realtime Client: Received Gemini BidiGenerateContentSetupComplete message, session established.");
                            await (StateChangedAsync?.Invoke(_webSocket.State, "Received Gemini BidiGenerateContentSetupComplete message, session established.") ?? Task.CompletedTask).ConfigureAwait(false);
                            continue;
                        }

                        await (MessageReceivedAsync?.Invoke(message) ?? Task.CompletedTask).ConfigureAwait(false);
                    }
                    // 如果 Gemini API 返回二进制数据，需要在此处添加处理逻辑
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Log.Information("Google Realtime Client: Receive loop for {EndpointUri} was canceled.", EndpointUri);
                await (StateChangedAsync?.Invoke(_webSocket.State, "Receive loop canceled by client") ?? Task.CompletedTask).ConfigureAwait(false);
            }
            catch (WebSocketException ex)
            {
                Log.Error(ex, "Google Realtime Client: WebSocketException in receive loop for {EndpointUri}.", EndpointUri);
                await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask).ConfigureAwait(false);
                await (StateChangedAsync?.Invoke(_webSocket.State, $"WebSocketException: {ex.Message}") ?? Task.CompletedTask).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Google Realtime Client: Unexpected error in receive loop for {EndpointUri}.", EndpointUri);
                await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask).ConfigureAwait(false);
                await (StateChangedAsync?.Invoke(_webSocket.State, $"Unexpected error: {ex.Message}") ?? Task.CompletedTask).ConfigureAwait(false);
            }
            finally
            {
                Log.Debug("Google Realtime Client: Receive loop for {EndpointUri} ended. Final WebSocket state: {State}", EndpointUri, _webSocket?.State);
                if (_webSocket?.State != WebSocketState.Closed && _webSocket?.State != WebSocketState.Aborted)
                {
                    Log.Warning("Google Realtime Client: Receive loop ended for {EndpointUri} but socket state is {State}.", EndpointUri, _webSocket?.State);
                }
            }
        }

        public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                var ex = new InvalidOperationException($"Google Realtime Client: WebSocket is not open for sending. Current state: {_webSocket?.State}");
                Log.Error(ex, "Google Realtime Client: Failed to send message to {EndpointUri}.", EndpointUri);
                await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask).ConfigureAwait(false);
                throw ex;
            }

            if (string.IsNullOrEmpty(message))
            {
                var ex = new ArgumentNullException(nameof(message));
                Log.Error(ex, "Google Realtime Client: Message cannot be null or empty.");
                await (ErrorOccurredAsync?.Invoke(ex) ?? Task.CompletedTask).ConfigureAwait(false);
                throw ex;
            }

            var messageBytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            // Log.Verbose("Google Realtime Client: Sent message to {EndpointUri}: {Message}", EndpointUri, message);
        }

        public async Task DisconnectAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            if (_webSocket == null)
            {
                Log.Information("Google Realtime Client: Disconnect called but WebSocket is null (not connected or already disposed).");
                return;
            }
            Log.Information("Google Realtime Client: Disconnecting from {EndpointUri}. Reason: {Reason}", EndpointUri, statusDescription);

            _receiveLoopCts?.Cancel();

            if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(5));
                    await _webSocket.CloseOutputAsync(closeStatus, statusDescription, cts.Token).ConfigureAwait(false);
                    Log.Information("Google Realtime Client: CloseOutputAsync completed for {EndpointUri}.", EndpointUri);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Log.Warning("Google Realtime Client: DisconnectAsync was cancelled by caller token during CloseOutputAsync for {EndpointUri}.", EndpointUri);
                }
                catch (OperationCanceledException ex)
                {
                    Log.Warning(ex, "Google Realtime Client: Timeout or cancellation during CloseOutputAsync for {EndpointUri}.", EndpointUri);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Google Realtime Client: Error during WebSocket CloseOutputAsync for {EndpointUri}.", EndpointUri);
                }
            }
            else
            {
                Log.Information("Google Realtime Client: WebSocket for {EndpointUri} not in Open/CloseReceived state ({State}), skipping CloseOutputAsync.", EndpointUri, _webSocket.State);
            }

            if (_receiveLoopTask is { IsCompleted: false })
            {
                try
                {
                    await Task.WhenAny(_receiveLoopTask, Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None)).ConfigureAwait(false);
                    if (!_receiveLoopTask.IsCompleted)
                    {
                        Log.Warning("Google Realtime Client: Receive loop for {EndpointUri} did not complete after DisconnectAsync.", EndpointUri);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Google Realtime Client: Exception while waiting for receive loop to complete during disconnect of {EndpointUri}.", EndpointUri);
                }
            }

            await (StateChangedAsync?.Invoke(_webSocket.State, statusDescription) ?? Task.CompletedTask).ConfigureAwait(false);
            // Final cleanup is handled by DisposeAsync or when a new connection is made.
        }

        private async Task CleanUpCurrentConnectionAsync(string reason)
        {
            Log.Debug("Google Realtime Client: Cleaning up current connection. Reason: {Reason}", reason);

            if (_receiveLoopCts is { IsCancellationRequested: false }) await _receiveLoopCts.CancelAsync().ConfigureAwait(false);

            if (_receiveLoopTask is { IsCompleted: false })
            {
                Log.Debug("Google Realtime Client: Waiting for previous receive loop to complete during cleanup.");
                try { await Task.WhenAny(_receiveLoopTask, Task.Delay(1000)).ConfigureAwait(false); } catch { /* ignore */ }
            }
            _receiveLoopTask = null;

            if (_webSocket != null)
            {
                if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
                {
                    try { await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, $"Cleaning up: {reason}", CancellationToken.None).ConfigureAwait(false); } catch { /* ignore */ }
                }
                _webSocket.Dispose();
            }
            _webSocket = null;

            if (_receiveLoopCts != null)
            {
                _receiveLoopCts.Dispose();
                _receiveLoopCts = null;
            }
            Log.Debug("Google Realtime Client: Cleanup complete.");
        }

        public async ValueTask DisposeAsync()
        {
            Log.Information("Google Realtime Client: Disposing client for {EndpointUri}.", EndpointUri);
            await DisconnectAsync(WebSocketCloseStatus.NormalClosure, "Client disposing", CancellationToken.None).ConfigureAwait(false);
            await CleanUpCurrentConnectionAsync("Disposing client.");
            GC.SuppressFinalize(this);
        }
    }
}