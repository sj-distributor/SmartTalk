using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantsRequest : IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
    
    public AiSpeechAssistantChannel? Channel { get; set; }
    
    public int? AgentId { get; set; }
}

public class GetAiSpeechAssistantsResponse : SmartTalkResponse<GetAiSpeechAssistantsResponseData>
{
}

public class GetAiSpeechAssistantsResponseData
{
    public int Count { get; set; }
    
    public List<AiSpeechAssistantDto> Assistants { get; set; }
}