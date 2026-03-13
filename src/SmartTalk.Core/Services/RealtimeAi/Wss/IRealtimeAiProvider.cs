using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.wss;

public interface IRealtimeAiProvider : IScopedDependency
{
    RealtimeAiProvider Provider { get; }
}