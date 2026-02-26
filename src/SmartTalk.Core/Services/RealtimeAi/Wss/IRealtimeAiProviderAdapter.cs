using SmartTalk.Core.Services.RealtimeAi.Services;
using SmartTalk.Core.Services.RealtimeAi.Wss;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.wss;

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