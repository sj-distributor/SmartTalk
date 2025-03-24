using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAssistantNumberRequest : IRequest
{
    public int NumberId { get; set; }
}

public class GetAssistantNumberResponse : SmartTalkResponse<NumberPoolDto>
{
}