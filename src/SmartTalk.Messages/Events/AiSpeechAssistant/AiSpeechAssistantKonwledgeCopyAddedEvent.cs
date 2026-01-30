using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Events.AiSpeechAssistant;

public class AiSpeechAssistantKonwledgeCopyAddedEvent : IEvent
{
    public string CopyJson { get; set; }
    
    public List<AiSpeechAssistantKnowledgeOldState> KnowledgeOldJsons { get; set; } = new();
}

public class AiSpeechAssistantKnowledgeOldState
{
    public int KnowledgeId { get; set; }
    
    public string OldMergedJson { get; set; }
}