using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.RealtimeAiV2;

public interface IRealtimeAiProvider : IScopedDependency
{
    AiSpeechAssistantProvider Provider { get; }
}