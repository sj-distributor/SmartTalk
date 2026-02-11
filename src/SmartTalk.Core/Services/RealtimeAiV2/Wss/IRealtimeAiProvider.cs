using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.RealtimeAiV2.Wss;

public interface IRealtimeAiProvider : IScopedDependency
{
    AiSpeechAssistantProvider Provider { get; }
}