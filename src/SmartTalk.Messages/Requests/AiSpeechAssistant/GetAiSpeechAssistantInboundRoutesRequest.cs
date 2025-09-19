using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantInboundRoutesRequest : IRequest
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    
    public string Keyword { get; set; }
}

public class GetAiSpeechAssistantInboundRoutesResponse : SmartTalkResponse<List<AiSpeechAssistantInboundRouteDto>>;