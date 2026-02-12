using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2;

public interface IRealtimeAiProviderAdapter : IScopedDependency
{
    Dictionary<string, string> GetHeaders(RealtimeAiServerRegion region);

    object BuildSessionConfig(RealtimeSessionOptions options);
    
    string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData);
    
    string BuildTextUserMessage(string text, string sessionId);

    object BuildInterruptMessage(string lastAssistantItemIdToInterrupt);

    /// <summary>
    /// Returns a JSON message to trigger an AI response after sending text,
    /// or null if the provider auto-triggers responses.
    /// </summary>
    string BuildTriggerResponseMessage();

    ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage);

    AiSpeechAssistantProvider Provider { get; }
}