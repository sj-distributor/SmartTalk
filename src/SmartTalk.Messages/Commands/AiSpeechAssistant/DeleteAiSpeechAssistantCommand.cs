using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class DeleteAiSpeechAssistantCommand : ICommand
{
    public List<int> AssistantIds { get; set; }
}

public class DeleteAiSpeechAssistantResponse : SmartiesResponse<List<AiSpeechAssistantDto>>
{
}