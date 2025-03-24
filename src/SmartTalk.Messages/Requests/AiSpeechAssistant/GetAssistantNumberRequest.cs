using Mediator.Net.Contracts;
using Smarties.Messages.DTO.SmartAssistant.Domain;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAssistantNumberRequest : IRequest
{
    public int AssistantId { get; set; }
}

public class GetAssistantNumberResponse : SmartTalkResponse<NumberPoolDto>
{
}

public class GetAssistantNumberResponseData
{
    public NumberPoolDto Number { get; set; }
    
    public AssistantDto Assistant { get; set; }
}