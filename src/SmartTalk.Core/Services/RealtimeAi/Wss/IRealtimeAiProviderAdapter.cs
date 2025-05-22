using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.wss;

public interface IRealtimeAiProviderAdapter : IRealtimeAiProvider
{
    Dictionary<string, string> GetHeaders();
    
    Task<object> GetInitialSessionPayloadAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistantProfile, string initialUserPrompt = null, string sessionId = null,
        RealtimeAiAudioCodec inputFormat = RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec outputFormat = RealtimeAiAudioCodec.PCM16, CancellationToken cancellationToken = default);

    string BuildGreetingMessage(string greeting);
    
    string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData);
    
    string BuildTextUserMessage(string text, string sessionId);

    object BuildInterruptMessage(string lastAssistantItemIdToInterrupt);
    
    ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage);
}