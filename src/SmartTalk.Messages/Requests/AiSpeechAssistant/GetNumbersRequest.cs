using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetNumbersRequest : IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
}

public class GetNumbersResponse : SmartTalkResponse<GetNumbersResponseData>
{
}

public class GetNumbersResponseData
{
    public int Count { get; set; }
    
    public List<NumberPoolDto> Numbers { get; set; }
}