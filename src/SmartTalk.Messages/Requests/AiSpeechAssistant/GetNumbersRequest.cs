using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetNumbersRequest : IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
}

public class GetNumbersResponse : SmartiesResponse<GetNumbersResponseData>
{
}

public class GetNumbersResponseData
{
    public int Count { get; set; }
    
    public List<NumberPoolDto> Numbers { get; set; }
}