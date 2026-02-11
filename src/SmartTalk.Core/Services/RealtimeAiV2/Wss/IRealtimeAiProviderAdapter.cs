using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Wss;

public interface IRealtimeAiProviderAdapter : IRealtimeAiProvider
{
    Dictionary<string, string> GetHeaders(RealtimeAiServerRegion region);

    Task<object> GetInitialSessionPayloadAsync(
        RealtimeSessionOptions options, string sessionId = null, CancellationToken cancellationToken = default);
    
    string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData);
    
    string BuildTextUserMessage(string text, string sessionId);

    object BuildInterruptMessage(string lastAssistantItemIdToInterrupt);
    
    ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage);
}