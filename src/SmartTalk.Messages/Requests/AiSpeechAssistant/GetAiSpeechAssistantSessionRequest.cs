using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantSessionRequest : IRequest
{
    public Guid SessionId { get; set; }
}

public class GetAiSpeechAssistantSessionResponse : SmartTalkResponse<AiSpeechAssistantSessionDto>
{
}