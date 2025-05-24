using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantSessionCommand : ICommand
{
    public Guid SessionId { get; set; }
}

public class UpdateAiSpeechAssistantSessionResponse : SmartTalkResponse<AiSpeechAssistantSessionDto>;