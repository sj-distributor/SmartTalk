using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAssistantByIdRequest : IRequest
{
    public int AssistantId { get; set; }
}

public class GetAssistantByIdResponse : SmartTalkResponse<AiSpeechAssistantDto>
{
}