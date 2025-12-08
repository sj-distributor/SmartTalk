using System.Net.WebSockets;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Services.RealtimeAi.wss;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.RealtimeAi.Wss;

public class RealtimeAiConversationEngine : IRealtimeAiConversationEngine
{
    private readonly IRealtimeAiProviderAdapter _aiAdapter;
    private readonly IRealtimeAiWssClient _realtimeAiClient;

    private string _sessionId;
    private string _greetings;
    public string CurrentSessionId { get; }
    private CancellationTokenSource _sessionCts; // 用于控制当前会话的生命周期 (For controlling the lifecycle of the current session)
    private Domain.AISpeechAssistant.AiSpeechAssistant _currentAssistantProfile; // 保存当前会话的助手配置 (Store current session's assistant profile)
    
    public event Func<RealtimeAiWssEventType, object, Task> SessionStatusChangedAsync;
    public event Func<RealtimeAiWssAudioData, Task> AiAudioOutputReadyAsync;
    public event Func<RealtimeAiWssTranscriptionData, Task> InputAudioTranscriptionPartialAsync;
    public event Func<RealtimeAiWssTranscriptionData, Task> OutputAudioTranscriptionPartialAsync;
    public event Func<RealtimeAiWssTranscriptionData, Task> InputAudioTranscriptionCompletedAsync;
    public event Func<RealtimeAiWssTranscriptionData, Task> OutputAudioTranscriptionCompletedyAsync;
    public event Func<RealtimeAiErrorData, Task> ErrorOccurredAsync;
    public event Func<Task> AiDetectedUserSpeechAsync;
    public event Func<string, Task> AiResponseInterruptedAsync;
    public event Func<object, Task> AiTurnCompletedAsync;
    public event Func<RealtimeAiWssFunctionCallData, Task> FunctionCallSuggestedAsync;
    public event Func<string, Task> AiRawMessageReceivedAsync;
    
    public RealtimeAiConversationEngine(IRealtimeAiProviderAdapter aiAdapter, IRealtimeAiWssClient realtimeAiClient)
    {
        _greetings = string.Empty;
        _aiAdapter = aiAdapter ?? throw new ArgumentNullException(nameof(aiAdapter));
        _realtimeAiClient = realtimeAiClient ?? throw new ArgumentNullException(nameof(realtimeAiClient));

        _realtimeAiClient.MessageReceivedAsync += OnClientMessageReceivedAsync;
        _realtimeAiClient.StateChangedAsync += OnClientStateChangedAsync;
        _realtimeAiClient.ErrorOccurredAsync += OnClientErrorOccurredAsync;
    }

    public async Task StartSessionAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistantProfile,
        string initialUserPrompt, RealtimeAiAudioCodec inputFormat, RealtimeAiAudioCodec outputFormat, RealtimeAiServerRegion region, CancellationToken cancellationToken)
    {
        // ... (启动逻辑同前) ...
        // ... (Startup logic same as before) ...
        if (assistantProfile == null) throw new ArgumentNullException(nameof(assistantProfile));
        if (string.IsNullOrEmpty(assistantProfile.ModelUrl))
            throw new ArgumentException("Assistant profile must have a ModelUrl specified.", nameof(assistantProfile));

        if (_sessionCts != null && !_sessionCts.IsCancellationRequested)
        {
            Log.Warning("AiConversationEngine: 会话已在进行中 (ID: {SessionId})。请先结束当前会话。", _sessionId); // AiConversationEngine: Session already in progress (ID: {SessionId}). Please end the current session first.
            await OnErrorOccurredAsync(new RealtimeAiErrorData { Code = "SessionInProgress", Message = "会话已在进行中" }); // Session already in progress
            return;
        }
        _currentAssistantProfile = assistantProfile; // 保存配置 (Save profile)
        var aiProviderServiceUri = new Uri(_currentAssistantProfile.ModelUrl);
        var connectionHeaders = _aiAdapter.GetHeaders(region);

        _sessionId = Guid.NewGuid().ToString("N"); // 生成一个新的会话 ID (Generate a new session ID)
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Log.Information("AiConversationEngine: 开始新会话 ID: {SessionId} for Provider: {Provider}, URL: {Url}", _sessionId, _currentAssistantProfile.ModelProvider, aiProviderServiceUri); // AiConversationEngine: Starting new session ID: {SessionId}
        await OnSessionStatusChangedAsync(RealtimeAiWssEventType.SessionInitializing, _sessionId);

        try
        {
            if (_realtimeAiClient.CurrentState != WebSocketState.Open || _realtimeAiClient.EndpointUri != aiProviderServiceUri)
            {
                Log.Information("AiConversationEngine: RealtimeAiClient 未连接或端点不同，尝试连接...", _realtimeAiClient.CurrentState, _realtimeAiClient.EndpointUri); // AiConversationEngine: RealtimeAiClient not connected or endpoint different, attempting to connect...
                await _realtimeAiClient.ConnectAsync(aiProviderServiceUri, connectionHeaders, _sessionCts.Token);
            }
            
            if (_realtimeAiClient.CurrentState != WebSocketState.Open)
            {
                throw new InvalidOperationException("无法连接到底层 Realtime AI Client。"); // Cannot connect to underlying Realtime AI Client.
            }

            var initialPayload = await _aiAdapter.GetInitialSessionPayloadAsync(_currentAssistantProfile, 
                new RealtimeAiEngineContext { InitialPrompt = initialUserPrompt, InputFormat = inputFormat, OutputFormat = outputFormat }, _sessionId, _sessionCts.Token);
            var initialMessageJson = JsonConvert.SerializeObject(initialPayload, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
            
            await _realtimeAiClient.SendMessageAsync(initialMessageJson, _sessionCts.Token);

            if (!string.IsNullOrEmpty(assistantProfile.Knowledge?.Greetings))
                _greetings = assistantProfile.Knowledge?.Greetings;
            
            Log.Information("AiConversationEngine: 已发送初始会话消息。会话 ID: {SessionId}", _sessionId); // AiConversationEngine: Initial session message sent. Session ID: {SessionId}
        }
        catch (OperationCanceledException) when (_sessionCts.IsCancellationRequested)
        {
            Log.Information("AiConversationEngine: 会话 (ID: {SessionId}) 启动被取消。", _sessionId); // AiConversationEngine: Session (ID: {SessionId}) startup was canceled.
            await OnSessionStatusChangedAsync(RealtimeAiWssEventType.SessionUpdateFailed, "会话启动被取消"); // Session startup was canceled
            await CleanupSessionAsync("启动被取消"); // Startup was canceled
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AiConversationEngine: 启动会话 (ID: {SessionId}) 失败。", _sessionId); // AiConversationEngine: Failed to start session (ID: {SessionId}).
            await OnErrorOccurredAsync(new RealtimeAiErrorData { Code = "SessionStartFailed", Message = ex.Message, IsCritical = true });
            await OnSessionStatusChangedAsync(RealtimeAiWssEventType.SessionUpdateFailed, ex.Message);
            await CleanupSessionAsync($"启动失败: {ex.Message}"); // Startup failed:
        }
    }
    
    private string BuildGreetingMessage(string greeting)
    {
        var message = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "user",
                content = new[]
                {
                    new
                    {
                        type = "input_text",
                        text = $"Greet the user with: '{greeting}'"
                    }
                }
            }
        };
            
        return JsonSerializer.Serialize(message);
    }
    
    private async Task OnClientMessageReceivedAsync(string rawMessage)
    {
        if (_sessionCts == null || _sessionCts.IsCancellationRequested) return; // 会话已结束或正在结束 (Session has ended or is ending)

        await (AiRawMessageReceivedAsync?.Invoke(rawMessage) ?? Task.CompletedTask); // 触发原始消息事件 (Trigger raw message event)

        var parsedEvent = _aiAdapter.ParseMessage(rawMessage);
        // Log.Debug("AiConversationEngine: 处理来自适配器的事件: {EventType}, ItemId: {ItemId}", parsedEvent.Type, parsedEvent.ItemId); // AiConversationEngine: Processing event from adapter: {EventType}, ItemId: {ItemId}

        try
        {
            switch (parsedEvent.Type)
            {
                 case RealtimeAiWssEventType.SessionInitialized:
                     Log.Information("AiConversationEngine: AI 服务商确认会话 (ID: {SessionId}) 已初始化/更新。", _sessionId); // AiConversationEngine: AI service provider confirmed session (ID: {SessionId}) initialized/updated.
                     
                     if (!string.IsNullOrEmpty(_greetings))
                     {
                         Log.Information("AiConversationEngine: 发送初始会话问候消息。会话 ID: {SessionId}", _sessionId);

                         await SendTextAsync($"Greet the user with: {_greetings}");
                         if (_currentAssistantProfile.ModelProvider == AiSpeechAssistantProvider.OpenAi)
                             await _realtimeAiClient.SendMessageAsync(JsonSerializer.Serialize(new { type = "response.create" }), _sessionCts.Token);
                     }
                     await OnSessionStatusChangedAsync(RealtimeAiWssEventType.SessionInitialized, parsedEvent.Data ?? _sessionId);
                     break;
                 case RealtimeAiWssEventType.ResponseAudioDelta:
                     // 直接将 Adapter 解析出的包含原始 Codec/SampleRate 的 GenericAudioData 传递出去
                     // Directly pass the GenericAudioData containing original Codec/SampleRate parsed by Adapter
                     if (parsedEvent.Data is RealtimeAiWssAudioData audioData)
                         await (AiAudioOutputReadyAsync?.Invoke(audioData) ?? Task.CompletedTask);
                     break;
                 case RealtimeAiWssEventType.InputAudioTranscriptionPartial:
                     if (parsedEvent.Data is RealtimeAiWssTranscriptionData inputTranscriptionPartialData)
                         await (InputAudioTranscriptionPartialAsync?.Invoke(inputTranscriptionPartialData) ?? Task.CompletedTask);
                     break;
                 case RealtimeAiWssEventType.InputAudioTranscriptionCompleted:
                     if (parsedEvent.Data is RealtimeAiWssTranscriptionData inputTranscriptionCompletedData)
                         await (InputAudioTranscriptionCompletedAsync?.Invoke(inputTranscriptionCompletedData) ?? Task.CompletedTask);
                     break;
                 case RealtimeAiWssEventType.OutputAudioTranscriptionPartial:
                     if (parsedEvent.Data is RealtimeAiWssTranscriptionData outputTranscriptionPartialData)
                         await (OutputAudioTranscriptionPartialAsync?.Invoke(outputTranscriptionPartialData) ?? Task.CompletedTask);
                     break;
                 case RealtimeAiWssEventType.OutputAudioTranscriptionCompleted:
                     if (parsedEvent.Data is RealtimeAiWssTranscriptionData outputTranscriptionCompletedData)
                         await (OutputAudioTranscriptionCompletedyAsync?.Invoke(outputTranscriptionCompletedData) ?? Task.CompletedTask);
                     break;
                 case RealtimeAiWssEventType.SpeechDetected:
                     await (AiDetectedUserSpeechAsync?.Invoke() ?? Task.CompletedTask);
                     break;
                 case RealtimeAiWssEventType.ResponseTurnCompleted:
                     await (AiTurnCompletedAsync?.Invoke(parsedEvent.Data ?? parsedEvent.RawJson) ?? Task.CompletedTask);
                     break;
                 case RealtimeAiWssEventType.FunctionCallSuggested:
                     if (parsedEvent.Data is RealtimeAiWssFunctionCallData funcData)
                         await (FunctionCallSuggestedAsync?.Invoke(funcData) ?? Task.CompletedTask);
                     break;
                 case RealtimeAiWssEventType.Error:
                     if (parsedEvent.Data is RealtimeAiErrorData errData)
                     {
                         Log.Error("AiConversationEngine: 收到来自 AI 服务商的错误: Code={ErrorCode}, Message={ErrorMessage}, Critical={IsCritical}", errData.Code, errData.Message, errData.IsCritical); // AiConversationEngine: Received error from AI service provider: Code={ErrorCode}, Message={ErrorMessage}, Critical={IsCritical}
                         await OnErrorOccurredAsync(errData);
                         if (errData.IsCritical) await EndSessionAsync($"严重错误: {errData.Message}"); // Critical error:
                     }
                     break;
                 case RealtimeAiWssEventType.ResponseAudioDone: 
                     Log.Information("AiConversationEngine: AI 音频响应结束 (ItemId: {ItemId})", parsedEvent.ItemId); // AiConversationEngine: AI audio response finished (ItemId: {ItemId})
                     break;
                 case RealtimeAiWssEventType.Unknown:
                     Log.Warning("AiConversationEngine: 收到未处理的 AI 事件类型: {OriginalType}, Raw: {RawJson}", parsedEvent.Data, parsedEvent.RawJson); // AiConversationEngine: Received unhandled AI event type: {OriginalType}, Raw: {RawJson}
                     break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AiConversationEngine: 处理 AI 消息时发生错误。消息类型: {ParsedEventType}, Raw: {RawMessage}", parsedEvent.Type, rawMessage); // AiConversationEngine: Error occurred while processing AI message. Message type: {ParsedEventType}, Raw: {RawMessage}
            await OnErrorOccurredAsync(new RealtimeAiErrorData { Code = "MessageProcessingError", Message = ex.Message, IsCritical = false });
        }
    }

    private async Task OnClientStateChangedAsync(WebSocketState newState, string reason)
    {
        // ... (同前) ...
         Log.Information("AiConversationEngine: 底层客户端连接状态改变为 {NewState}。原因: {Reason}。会话 ID: {SessionId}", newState, reason, _sessionId); // AiConversationEngine: Underlying client connection state changed to {NewState}. Reason: {Reason}. Session ID: {SessionId}
         await OnSessionStatusChangedAsync(RealtimeAiWssEventType.ConnectionStateChanged, new { State = newState, Reason = reason });

         if (newState == WebSocketState.Closed || newState == WebSocketState.Aborted)
         {
             if (_sessionCts is { IsCancellationRequested: false }) // 如果不是由 EndSessionAsync 主动关闭的 (If not actively closed by EndSessionAsync)
             {
                 Log.Warning("AiConversationEngine: 底层连接意外关闭。会话 (ID: {SessionId}) 将被终止。", _sessionId); // AiConversationEngine: Underlying connection closed unexpectedly. Session (ID: {SessionId}) will be terminated.
                 await OnErrorOccurredAsync(new RealtimeAiErrorData { Code = "ConnectionLost", Message = $"底层连接丢失: {reason}", IsCritical = true }); // Underlying connection lost:
                 await CleanupSessionAsync($"底层连接丢失: {reason}"); // 确保会话状态更新和资源清理 (Underlying connection lost:)
             }
         }
    }

    private async Task OnClientErrorOccurredAsync(Exception ex)
    {
        // ... (同前) ...
         Log.Error(ex, "AiConversationEngine: 底层客户端报告错误。会话 ID: {SessionId}", _sessionId); // AiConversationEngine: Underlying client reported error. Session ID: {SessionId}
         await OnErrorOccurredAsync(new RealtimeAiErrorData { Code = "RealtimeClientError", Message = ex.Message, IsCritical = true }); // 假设客户端错误是严重的 (Assuming client error is critical)
         if (_sessionCts != null && !_sessionCts.IsCancellationRequested)
         {
              await EndSessionAsync($"底层客户端错误: {ex.Message}"); // Underlying client error:
         }
    }

    public async Task SendAudioChunkAsync(RealtimeAiWssAudioData audioData)
    {
        // **重要:** 此方法期望接收到的 audioData 已经是 AI 服务提供商期望的格式
        // (例如，已经被 TwilioStreamHandler 使用 IAudioCodecAdapter 转换过)
        // **Important:** This method expects the received audioData to already be in the format expected by the AI service provider
        // (e.g., already converted by TwilioStreamHandler using IAudioCodecAdapter)
        if (!IsSessionActive("发送音频块")) return; // Send audio chunk
        
        // _logger.LogTrace("AiConversationEngine: 准备发送 {Codec} @ {Rate}Hz 音频块。会话 ID: {SessionId}", audioData.Codec, audioData.SampleRate, _sessionId); // AiConversationEngine: Preparing to send {Codec} @ {Rate}Hz audio chunk. Session ID: {SessionId}
        var messageJson = _aiAdapter.BuildAudioAppendMessage(audioData);
        await _realtimeAiClient.SendMessageAsync(messageJson, _sessionCts.Token);
    }

    public async Task SendTextAsync(string text)
    {
        Log.Information("AiConversationEngine: 准备发送文本消息: '{Text}'. 会话 ID: {SessionId}", text, _sessionId); // AiConversationEngine: Preparing to send text message: '{Text}'. Session ID: {SessionId}
        var messageJson = _aiAdapter.BuildTextUserMessage(text, _sessionId);
        await _realtimeAiClient.SendMessageAsync(messageJson, _sessionCts.Token);
        await _realtimeAiClient.SendMessageAsync(JsonSerializer.Serialize(new { type = "response.create" }), _sessionCts.Token);
    }

    public async Task NotifyUserSpeechStartedAsync(string lastAssistantItemIdToInterrupt = null)
    {
        // ... (同前) ...
        if (!IsSessionActive("通知用户开始说话")) return; // Notify user started speaking

        Log.Information("AiConversationEngine: 用户开始说话，尝试打断 AI (LastItemId: {LastItemId})。会话 ID: {SessionId}", lastAssistantItemIdToInterrupt, _sessionId); // AiConversationEngine: User started speaking, attempting to interrupt AI (LastItemId: {LastItemId}). Session ID: {SessionId}
        var interruptPayload = _aiAdapter.BuildInterruptMessage(lastAssistantItemIdToInterrupt);
        if (interruptPayload != null)
        {
            var messageJson = interruptPayload is string s ? s : JsonSerializer.Serialize(interruptPayload);
            await _realtimeAiClient.SendMessageAsync(messageJson, _sessionCts.Token);
            await (AiResponseInterruptedAsync?.Invoke(lastAssistantItemIdToInterrupt) ?? Task.CompletedTask);
        } else {
            Log.Information("AiConversationEngine: AI Provider Adapter 未提供打断消息，可能不支持显式打断。"); // AiConversationEngine: AI Provider Adapter did not provide interrupt message, may not support explicit interruption.
        }
    }
    
    private bool IsSessionActive(string operationName)
    {
        // ... (同前) ...
        if (_sessionCts == null || _sessionCts.IsCancellationRequested)
        {
            Log.Warning("AiConversationEngine: 会话 (ID: {SessionId}) 未激活或已结束，无法执行操作 '{OperationName}'。", _sessionId, operationName); // AiConversationEngine: Session (ID: {SessionId}) not active or ended, cannot perform operation '{OperationName}'.
            return false;
        }
        return true;
    }

    public async Task EndSessionAsync(string reason)
    {
        // ... (同前) ...
        Log.Information("AiConversationEngine: 正在结束会话 (ID: {SessionId})。原因: {Reason}", _sessionId, reason); // AiConversationEngine: Ending session (ID: {SessionId}). Reason: {Reason}
        if (_sessionCts == null)
        {
            Log.Warning("AiConversationEngine: 尝试结束一个未启动或已清理的会话 (ID: {SessionId})。", _sessionId); // AiConversationEngine: Attempting to end a session that was not started or already cleaned up (ID: {SessionId}).
            return;
        }
         
        await CleanupSessionAsync(reason); // 调用集中的清理逻辑 (Call centralized cleanup logic)

        if (_realtimeAiClient.CurrentState == WebSocketState.Open)
        {
            Log.Information("AiConversationEngine: 会话 (ID: {SessionId}) 结束，正在断开 RealtimeAiClient 连接。", _sessionId); // AiConversationEngine: Session (ID: {SessionId}) ended, disconnecting RealtimeAiClient connection.
            await _realtimeAiClient.DisconnectAsync(WebSocketCloseStatus.NormalClosure, $"会话结束: {reason}", CancellationToken.None); // 使用新的 CancellationToken (Session ended:)
        }
    }
    
    private async Task CleanupSessionAsync(string reason)
    {
        // ... (同前) ...
        if (_sessionCts is { IsCancellationRequested: false })
        {
            await _sessionCts.CancelAsync(); // 触发取消 (Trigger cancellation)
        }
        await OnSessionStatusChangedAsync(RealtimeAiWssEventType.SessionUpdateFailed, $"会话已结束/失败: {reason}"); // 使用 SessionUpdateFailed 或自定义一个 SessionEnded (Session ended/failed:)
        Log.Information("AiConversationEngine: 会话 (ID: {SessionId}) 清理完成。原因: {Reason}", _sessionId, reason); // AiConversationEngine: Session (ID: {SessionId}) cleanup complete. Reason: {Reason}
        _currentAssistantProfile = null; // 清理当前助手配置 (Clear current assistant profile)
    }
    
    private async Task OnSessionStatusChangedAsync(RealtimeAiWssEventType type, object data) => await (SessionStatusChangedAsync?.Invoke(type, data) ?? Task.CompletedTask);
    private async Task OnErrorOccurredAsync(RealtimeAiErrorData errorData) => await (ErrorOccurredAsync?.Invoke(errorData) ?? Task.CompletedTask);

    public async ValueTask DisposeAsync()
    {
        // ... (同前) ...
        Log.Information("AiConversationEngine: 正在释放 (会话 ID: {SessionId})。", _sessionId); // AiConversationEngine: Disposing (Session ID: {SessionId}).
         
        if (_realtimeAiClient != null)
        {
            _realtimeAiClient.MessageReceivedAsync -= OnClientMessageReceivedAsync;
            _realtimeAiClient.StateChangedAsync -= OnClientStateChangedAsync;
            _realtimeAiClient.ErrorOccurredAsync -= OnClientErrorOccurredAsync;
        }

        await EndSessionAsync("引擎正在释放"); // 确保会话结束逻辑被调用 (Engine disposing)

        _sessionCts?.Dispose(); // 现在可以安全地 Dispose (Can now safely Dispose)
        _sessionCts = null;

        Log.Information("AiConversationEngine: 释放完成 (会话 ID: {SessionId})。", _sessionId); // AiConversationEngine: Disposal complete (Session ID: {SessionId}).
        GC.SuppressFinalize(this);
    }
}