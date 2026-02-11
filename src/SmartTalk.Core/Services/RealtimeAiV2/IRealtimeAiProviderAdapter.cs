using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2;

public interface IRealtimeAiProviderAdapter : IScopedDependency
{
    Dictionary<string, string> GetHeaders(RealtimeAiServerRegion region);

    Task<object> GetInitialSessionPayloadAsync(
        RealtimeSessionOptions options, string sessionId = null, CancellationToken cancellationToken = default);
    
    string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData);
    
    string BuildTextUserMessage(string text, string sessionId);

    object BuildInterruptMessage(string lastAssistantItemIdToInterrupt);
    
    ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage);
    
    AiSpeechAssistantProvider Provider { get; }
}