using SmartTalk.Core.Services.RealtimeAi.Wss;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.wss;

public interface IRealtimeAiProviderAdapter : IRealtimeAiProvider
{
    Dictionary<string, string> GetHeaders();
    
    Task<object> GetInitialSessionPayloadAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistantProfile, RealtimeAiEngineContext context, string sessionId = null, CancellationToken cancellationToken = default);

    (string MessageJson, bool IsImage) BuildAudioAppendMessage(RealtimeAiWssAudioData audioData);
    
    string BuildTextUserMessage(string text, string sessionId);

    object BuildInterruptMessage(string lastAssistantItemIdToInterrupt);
    
    ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage);
}