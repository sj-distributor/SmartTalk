using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Events.AiSpeechAssistant;

public class AiSpeechAssistantKnowledgeAddedEvent : IEvent
{
    public AiSpeechAssistantKnowledgeDto PrevKnowledge { get; set; }
    
    public AiSpeechAssistantKnowledgeDto LatestKnowledge { get; set; }
}