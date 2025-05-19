using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.RealtimeAi.wss;

public interface IRealtimeAiProviderAdapter : IScopedDependency, IRealtimeAiProvider
{
    Dictionary<string, string> GetHeaders();
    
    Task<object> GetInitialSessionPayloadAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistantProfile, string initialUserPrompt = null,
        string sessionId = null, CancellationToken cancellationToken = default);
    
    string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData);
    
    string BuildTextUserMessage(string text, string sessionId);

    object BuildInterruptMessage(string lastAssistantItemIdToInterrupt);
    
    ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage);
}