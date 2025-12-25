using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class KonwledgeCopyCommand: ICommand
{
    public int SourceKnowledgeId { get; set; }
    
    public List<int> TargetKnowledgeId { get; set; }
    
    public bool IsSyncUpdate { get; set; }
}

public class KonwledgeCopyResponse : SmartTalkResponse<List<KnowledgeOldState>>
{
}