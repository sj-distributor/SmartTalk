using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantByIdRequest : IRequest
{
    public int AssistantId { get; set; }
}

public class GetAiSpeechAssistantByIdResponse : SmartTalkResponse<AiSpeechAssistantDto>
{
}