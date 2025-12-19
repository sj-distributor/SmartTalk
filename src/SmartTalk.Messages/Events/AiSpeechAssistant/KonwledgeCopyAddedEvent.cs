using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Events.AiSpeechAssistant;

public class KonwledgeCopyAddedEvent : IEvent
{
    public string CopyJson { get; set; }
    
    public List<KnowledgeOldState> KnowledgeOldJsons { get; set; } = new();
}

public class KnowledgeOldState
{
    public int KnowledgeId { get; set; }
    
    public string OldMergedJson { get; set; }
}

