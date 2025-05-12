using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.wss;

public interface IRealtimeAiProviderAdapter : IScopedDependency
{
    Task<object> GetInitialSessionPayloadAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistantProfile, string initialUserPrompt = null,
        string sessionId = null, CancellationToken cancellationToken = default);
    
    string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData);
    
    string BuildTextUserMessage(string text, string sessionId);
    
    ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage);
}