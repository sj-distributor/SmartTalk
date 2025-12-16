using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeCopy;

public class KonwledgeCopyCommand: ICommand
{
    public List<CopyKnowledge> CopyKnowledge { get; set; }    
    
    public int TargetKnowledgeId { get; set; }

}

public class KonwledgeCopyResponse : SmartTalkResponse<AiSpeechAssistantKnowledgeDto>
{
}

public class CopyKnowledge
{
    public string CopyKnowledgePoint { get; set; }
    
    public int SourceKnowledgeId { get; set; }
    
    public string RelatedFrom { get; set; }
    
    public bool IsSyncUpdate { get; set; }
}