using Serilog;
using System.Text;
using Newtonsoft.Json;
using SmartTalk.Core.Ioc;
using System.Net.WebSockets;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Core.Services.RealtimeAi.Wss;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAi.Adapters;

namespace SmartTalk.Core.Services.RealtimeAi.Services;

public interface ITwilioUtilService : IScopedDependency
{
    Task OnCallStartedAsync(
        ClientWebSocket webSocket, string callSid, RealtimeAiConnectionProfile connectionProfile,
        string initialPrompt, RealtimeAiAudioCodec inputFormat, RealtimeAiServerRegion region, RealtimeAiAudioCodec outputFormat);
}

public class TwilioUtilUtilService : ITwilioUtilService
{
    private readonly IRealtimeAiConversationEngine _aiEngine;
    private readonly IRealtimeAiAudioCodecAdapter _codecAdapter; // 注入 Codec 适配器 (Inject Codec adapter)
    
    private string _currentCallSid; // 示例：当前通话ID (Example: current call ID)
    private ClientWebSocket _webSocket;
    private RealtimeSessionOptions _currentOptions;
    
    private const int TwilioInputSampleRate = 8000;
    private const RealtimeAiAudioCodec TwilioInputCodec = RealtimeAiAudioCodec.MULAW;
    private const int TwilioOutputSampleRate = 8000;
    private const RealtimeAiAudioCodec TwilioOutputCodec = RealtimeAiAudioCodec.MULAW;

    public TwilioUtilUtilService(IRealtimeAiConversationEngine aiEngine, IRealtimeAiAudioCodecAdapter codecAdapter)
    {
        _aiEngine = aiEngine ?? throw new ArgumentNullException(nameof(aiEngine));
        _codecAdapter = codecAdapter ?? throw new ArgumentNullException(nameof(codecAdapter)); // 注入 (Inject)
        
        _aiEngine.SessionStatusChangedAsync += OnAiSessionStatusChangedAsync;
        _aiEngine.AiAudioOutputReadyAsync += OnAiAudioOutputReadyAsync;
        _aiEngine.ErrorOccurredAsync += OnAiErrorOccurredAsync;
        // _aiEngine.AiDetectedUserSpeechAsync += OnAiDetectedUserSpeechAsync;
        // _aiEngine.AiResponseInterruptedAsync += OnAiResponseInterruptedAsync;
        // _aiEngine.AiTurnCompletedAsync += OnAiTurnCompletedAsync;
        // _aiEngine.FunctionCallSuggestedAsync += OnAiFunctionCallSuggestedAsync;
        // _aiEngine.AiRawMessageReceivedAsync += OnAiRawMessageReceivedAsync;
    }
    
    public async Task OnCallStartedAsync(
        ClientWebSocket webSocket, string callSid, RealtimeAiConnectionProfile connectionProfile,
        string initialPrompt, RealtimeAiAudioCodec inputFormat, RealtimeAiServerRegion region, RealtimeAiAudioCodec outputFormat)
    {
        _webSocket = webSocket;
        _currentCallSid = callSid;
        _currentOptions = new RealtimeSessionOptions
        {
            WebSocket = webSocket,
            ConnectionProfile = connectionProfile,
            InitialPrompt = initialPrompt,
            InputFormat = inputFormat,
            OutputFormat = outputFormat,
            Region = region
        };
        Log.Information("TwilioHandler: 电话呼叫开始 CallSid: {CallSid}。准备启动 AI 会话。", callSid); // TwilioHandler: Call started CallSid: {CallSid}. Preparing to start AI session.
        try
        {
            await _aiEngine.StartSessionAsync(_currentOptions, CancellationToken.None); // 传入合适的 CancellationToken (Pass appropriate CancellationToken)
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TwilioHandler: 启动 AI 会话失败 CallSid: {CallSid}", callSid); // TwilioHandler: Failed to start AI session CallSid: {CallSid}
        }
    }
    
    // 当从 Twilio 收到音频流时 (When audio stream is received from Twilio)
    public async Task OnTwilioMediaReceivedAsync(string base64AudioPayload, string streamSid)
    {
        if (string.IsNullOrEmpty(base64AudioPayload)) return;
        
        if (_currentOptions == null)
        {
            Log.Warning("TwilioHandler: 收到 Twilio 音频但当前没有活动的助手配置 CallSid: {CallSid}", _currentCallSid); // TwilioHandler: Received Twilio audio but no active assistant profile for CallSid: {CallSid}
            return;
        }

        try
        {
            var twilioAudioBytes = Convert.FromBase64String(base64AudioPayload);
            
            // 确定 AI 期望的格式 (Determine the format expected by AI)

            byte[] audioBytesForAi;

            // 检查是否需要转换 (Check if conversion is needed)
            // if (TwilioInputCodec == aiTargetCodec && TwilioInputSampleRate == aiTargetSampleRate)
            if(_currentOptions.ModelConfig.Provider is AiSpeechAssistantProvider.OpenAi or AiSpeechAssistantProvider.Azure)
            {
                audioBytesForAi = twilioAudioBytes;
                // Log.Trace("TwilioHandler: Twilio 输入音频格式 ({Codec} @ {Rate}Hz) 与 AI 期望格式相同，无需转换。", TwilioInputCodec, TwilioInputSampleRate); // TwilioHandler: Twilio input audio format ({Codec} @ {Rate}Hz) is the same as AI expected format, no conversion needed.
            }
            else
            {
                Log.Debug("TwilioHandler: 需要将 Twilio 输入音频 (MULAW 8kHz) 转换为 AI 期望的格式 (PCM 16kHz)。CallSid: {CallSid}", _currentCallSid); // TwilioHandler: Need to convert Twilio input audio ({InputCodec} @ {InputRate}Hz) to AI expected format ({OutputCodec} @ {OutputRate}Hz). CallSid: {CallSid}
                
                // 使用 Codec 适配器进行转换 (Use Codec adapter for conversion)
                audioBytesForAi = await _codecAdapter.ConvertAsync(
                    twilioAudioBytes,
                    TwilioInputCodec,
                    TwilioInputSampleRate,
                    RealtimeAiAudioCodec.PCM16,
                    8000,
                    CancellationToken.None); // 使用合适的 CancellationToken (Use appropriate CancellationToken)
            }

            // 发送转换后的音频给 AI 引擎 (Send the converted audio to AI engine)
            await _aiEngine.SendAudioChunkAsync(new RealtimeAiWssAudioData { Base64Payload = Convert.ToBase64String(audioBytesForAi),
                 // AudioCodec = aiTargetCodec, // 发送给 AI 的是目标格式 (Sending the target format to AI)
                 // SampleRate = aiTargetSampleRate, // 发送给 AI 的是目标采样率 (Sending the target sample rate to AI)
                 // StreamSid = streamSid
                 });
        }
        catch (NotSupportedException nse)
        {
             Log.Error(nse, "TwilioHandler: 音频编解码器转换不支持 CallSid: {CallSid}", _currentCallSid); // TwilioHandler: Audio codec conversion not supported CallSid: {CallSid}
             // 处理错误，例如记录或通知
             // Handle error, e.g., log or notify
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TwilioHandler: 处理 Twilio 音频输入时出错 CallSid: {CallSid}", _currentCallSid); // TwilioHandler: Error processing Twilio audio input CallSid: {CallSid}
        }
    }
    
    // 当 Twilio 电话呼叫结束时 (When Twilio call ends)
    public async Task OnCallEndedAsync()
    {
        // ... (同前) ...
        Log.Information("TwilioHandler: 电话呼叫结束 CallSid: {CallSid}。准备结束 AI 会话。", _currentCallSid); // TwilioHandler: Call ended CallSid: {CallSid}. Preparing to end AI session.
        await _aiEngine.EndSessionAsync("电话呼叫结束"); // Call ended
        _currentCallSid = null;
        _currentOptions = null; // 清理配置 (Clear options)
    }
    
    // --- AI 引擎事件处理 (AI engine event handling) ---
    private Task OnAiSessionStatusChangedAsync(RealtimeAiWssEventType type, object data)
    {
        // ... (同前) ...
        Log.Information("TwilioHandler (CallSid: {CallSid}): AI 会话状态改变: {EventType}, 数据: {@EventData}", _currentCallSid, type, data); // TwilioHandler (CallSid: {CallSid}): AI session state changed: {EventType}, Data: {@EventData}
        if (type == RealtimeAiWssEventType.SessionInitialized)
        {
            Log.Information("TwilioHandler: AI 会话已成功初始化，可以开始双向通信。"); // TwilioHandler: AI session successfully initialized, bidirectional communication can begin.
        }
        else if (type == RealtimeAiWssEventType.SessionUpdateFailed)
        {
            Log.Error("TwilioHandler: AI 会话初始化或更新失败: {@EventData}", data); // TwilioHandler: AI session initialization or update failed: {@EventData}
        }
        return Task.CompletedTask;
    }
    
    private async Task OnAiAudioOutputReadyAsync(RealtimeAiWssAudioData aiAudioData)
    {
        if (aiAudioData == null || string.IsNullOrEmpty(aiAudioData.Base64Payload)) return;

        Log.Information("TwilioHandler (CallSid: {CallSid})，准备发送给 Twilio。ItemId: {ItemId}", _currentCallSid, aiAudioData.ItemId); // TwilioHandler (CallSid: {CallSid}): Received AI audio output ({Codec} @ {Rate}Hz), preparing to send to Twilio. ItemId: {ItemId}

        try
        {
            var aiAudioBytes = Convert.FromBase64String(aiAudioData.Base64Payload);
            byte[] audioBytesForTwilio;

            // 检查是否需要转换成 Twilio 期望的格式 (Check if conversion to Twilio's expected format is needed)
            if (_currentOptions.ModelConfig.Provider is AiSpeechAssistantProvider.OpenAi or AiSpeechAssistantProvider.Azure)
            {
                audioBytesForTwilio = aiAudioBytes;
                // Log.Trace("TwilioHandler: AI 输出音频格式 ({Codec} @ {Rate}Hz) 与 Twilio 期望格式相同，无需转换。", aiAudioData.Codec, aiAudioData.SampleRate); // TwilioHandler: AI output audio format ({Codec} @ {Rate}Hz) is the same as Twilio expected format, no conversion needed.
            }
            else
            {
                 Log.Debug("TwilioHandler: 需要将 AI 输出音频转换为 MULAW 格式 8kHz。CallSid: {CallSid}", _currentCallSid); // TwilioHandler: Need to convert AI output audio ({InputCodec} @ {InputRate}Hz) to Twilio expected format ({OutputCodec} @ {OutputRate}Hz). CallSid: {CallSid}
                
                // 使用 Codec 适配器进行转换 (Use Codec adapter for conversion)
                audioBytesForTwilio = await _codecAdapter.ConvertAsync(
                    aiAudioBytes,
                    RealtimeAiAudioCodec.PCM16,
                    24000,
                    TwilioOutputCodec,
                    TwilioOutputSampleRate,
                    CancellationToken.None); // 使用合适的 CancellationToken (Use appropriate CancellationToken)
            }
            
            // 实际代码：将转换后的 audioBytesForTwilio 通过 Twilio WebSocket 发送出去
            await SendToTwilioWebSocketAsync(Convert.ToBase64String(audioBytesForTwilio), _aiEngine.CurrentSessionId, CancellationToken.None);
            // Actual code: send the converted audioBytesForTwilio via Twilio WebSocket
             Log.Information("TwilioHandler (CallSid: {CallSid}): **模拟**发送 {Length} 字节的 {Codec} @ {Rate}Hz 音频到 Twilio。", _currentCallSid, audioBytesForTwilio.Length, TwilioOutputCodec, TwilioOutputSampleRate); // TwilioHandler (CallSid: {CallSid}): **Simulating** sending {Length} bytes of {Codec} @ {Rate}Hz audio to Twilio.

        }
        catch (NotSupportedException nse)
        {
             Log.Error(nse, "TwilioHandler: 音频编解码器转换不支持 CallSid: {CallSid}", _currentCallSid); // TwilioHandler: Audio codec conversion not supported CallSid: {CallSid}
        }
        catch (Exception ex)
        {
             Log.Error(ex, "TwilioHandler: 处理 AI 音频输出时出错 CallSid: {CallSid}", _currentCallSid); // TwilioHandler: Error processing AI audio output CallSid: {CallSid}
        }
    }
    
    private Task OnAiTranscriptionReadyAsync(RealtimeAiWssTranscriptionData txData)
    {
        // ... (同前) ...
        Log.Information("TwilioHandler (CallSid: {CallSid}): AI 转录结果 ({Speaker}, {Transcript}", _currentCallSid, txData.Speaker, txData.Transcript); // TwilioHandler (CallSid: {CallSid}): AI transcription result ({Speaker}, Final={IsFinal}): {Transcript}
        return Task.CompletedTask;
    }
    
    private Task OnAiErrorOccurredAsync(RealtimeAiErrorData errorData)
    {
        // ... (同前) ...
        Log.Error("TwilioHandler (CallSid: {CallSid}): AI 引擎报告错误: Code={ErrorCode}, Message={ErrorMessage}, Critical={IsCritical}", _currentCallSid, errorData.Code, errorData.Message, errorData.IsCritical); // TwilioHandler (CallSid: {CallSid}): AI engine reported error: Code={ErrorCode}, Message={ErrorMessage}, Critical={IsCritical}
        if (errorData.IsCritical)
        {
            Log.Warning("TwilioHandler: 发生严重 AI 错误，可能需要结束当前通话 CallSid: {CallSid}", _currentCallSid); // TwilioHandler: Critical AI error occurred, may need to end current call CallSid: {CallSid}
        }
        return Task.CompletedTask;
    }
    
    private async Task SendToTwilioWebSocketAsync(string message, string sessionId, CancellationToken cancellationToken)
    {
        var audioDelta = new
        {
            @event = "media",
            streamSid = sessionId,
            media = new { payload = message }
        };
        
        await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(audioDelta))), WebSocketMessageType.Text, true, cancellationToken);
    }
    
    // 清理逻辑 (例如，如果 TwilioStreamHandler 被释放)
    // Cleanup logic (e.g., if TwilioStreamHandler is disposed)
    public async ValueTask DisposeAsync()
    {
        // ... (同前) ...
        Log.Information("TwilioHandler (CallSid: {CallSid}): 正在释放。", _currentCallSid); // TwilioHandler (CallSid: {CallSid}): Disposing.
        if (!string.IsNullOrEmpty(_currentCallSid)) // 确保如果通话仍在进行，则结束会话 (Ensure session is ended if call is still in progress)
        {
            await OnCallEndedAsync();
        }
        if (_aiEngine != null)
        {
            _aiEngine.SessionStatusChangedAsync -= OnAiSessionStatusChangedAsync;
            _aiEngine.AiAudioOutputReadyAsync -= OnAiAudioOutputReadyAsync;
            _aiEngine.ErrorOccurredAsync -= OnAiErrorOccurredAsync;
        }
    }
}