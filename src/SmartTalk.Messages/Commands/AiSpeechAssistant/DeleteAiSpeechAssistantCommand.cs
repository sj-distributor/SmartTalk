using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class DeleteAiSpeechAssistantCommand : ICommand
{
    public List<int> AssistantIds { get; set; }
}

public class DeleteAiSpeechAssistantResponse : SmartTalkResponse<List<AiSpeechAssistantDto>>
{
}