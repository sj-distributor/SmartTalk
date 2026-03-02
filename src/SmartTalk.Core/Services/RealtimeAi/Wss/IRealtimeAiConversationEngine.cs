using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.RealtimeAi.Services;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.Wss;

public interface IRealtimeAiConversationEngine : IAsyncDisposable, IScopedDependency
{
    string CurrentSessionId { get; }
    event Func<RealtimeAiWssEventType, object, Task> SessionStatusChangedAsync;
    event Func<RealtimeAiWssAudioData, Task> AiAudioOutputReadyAsync; // **注意:** 此事件传递的音频数据应为 AI 服务原始输出格式 (Note: Audio data passed by this event should be in the AI service's original output format)
    event Func<RealtimeAiWssTranscriptionData, Task> InputAudioTranscriptionPartialAsync;
    event Func<RealtimeAiWssTranscriptionData, Task> OutputAudioTranscriptionPartialAsync;
    event Func<RealtimeAiWssTranscriptionData, Task> InputAudioTranscriptionCompletedAsync;
    event Func<RealtimeAiWssTranscriptionData, Task> OutputAudioTranscriptionCompletedyAsync;
    event Func<RealtimeAiErrorData, Task> ErrorOccurredAsync;
    event Func<Task> AiDetectedUserSpeechAsync;
    event Func<string, Task> AiResponseInterruptedAsync;
    event Func<object, Task> AiTurnCompletedAsync;
    event Func<RealtimeAiWssFunctionCallData, Task> FunctionCallSuggestedAsync;
    event Func<string, Task> AiRawMessageReceivedAsync;
    
    Task StartSessionAsync(RealtimeSessionOptions options, CancellationToken cancellationToken);
    /// <summary>
    /// 发送音频数据块给 AI。
    /// **注意:** 此方法接收的 audioData 应为 AI 服务提供商期望的格式 (可能已由外部转换)。
    /// Sends an audio data chunk to AI.
    /// **Note:** The audioData received by this method should be in the format expected by the AI service provider (potentially converted externally).
    /// </summary>
    Task SendAudioChunkAsync(RealtimeAiWssAudioData audioData);
    Task SendTextAsync(string text);
    Task NotifyUserSpeechStartedAsync(string lastAssistantItemIdToInterrupt = null);
    Task EndSessionAsync(string reason);
}