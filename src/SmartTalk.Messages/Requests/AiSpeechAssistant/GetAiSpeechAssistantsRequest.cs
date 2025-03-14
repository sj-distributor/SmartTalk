using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantsRequest : IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
}

public class GetAiSpeechAssistantsResponse : SmartiesResponse<GetAiSpeechAssistantsResponseData>
{
}

public class GetAiSpeechAssistantsResponseData
{
    public int Count { get; set; }
    
    public List<AiSpeechAssistantDto> Assistants { get; set; }
}