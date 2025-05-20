using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.RealtimeAi.wss;

public interface IRealtimeAiProvider : IScopedDependency
{
    AiSpeechAssistantProvider Provider { get; }
}