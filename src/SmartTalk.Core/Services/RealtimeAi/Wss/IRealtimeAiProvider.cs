using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.RealtimeAi.wss;

public interface IRealtimeAiProvider
{
    AiSpeechAssistantProvider Provider { get; }
}