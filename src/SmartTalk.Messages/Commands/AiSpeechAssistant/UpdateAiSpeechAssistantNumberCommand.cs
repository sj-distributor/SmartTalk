using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantNumberCommand : ICommand
{
    public int AssistantId { get; set; }
}

public class UpdateAiSpeechAssistantNumberResponse : SmartTalkResponse<NumberPoolDto>
{
}