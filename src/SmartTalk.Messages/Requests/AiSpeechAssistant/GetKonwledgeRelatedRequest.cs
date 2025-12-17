using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetKonwledgeRelatedRequest: IRequest
{
    public int AgentId { get; set; }
}

public class GetKonwledgeRelatedResponse : SmartTalkResponse<GetKonwledgeRelatedResponseData>
{
}

public class GetKonwledgeRelatedResponseData
{
    public List<AiSpeechAssistantKnowledgeDto> DedicatedknowledgeDtos { get; set; }
}
