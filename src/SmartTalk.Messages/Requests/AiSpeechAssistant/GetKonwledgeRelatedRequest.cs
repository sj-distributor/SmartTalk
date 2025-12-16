using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetKonwledgeRelatedRequest: IRequest
{
    public int TargetKnowledgeId { get; set; }
}

public class GetKonwledgeRelatedResponse : SmartTalkResponse<GetKonwledgeRelatedResponseData>
{
}

public class GetKonwledgeRelatedResponseData
{
    public List<KnowledgeCopyRelatedDto> KnowledgeCopyRelatedDtos { get; set; }
    
    public string DedicatedKnowledge { get; set; }
}
