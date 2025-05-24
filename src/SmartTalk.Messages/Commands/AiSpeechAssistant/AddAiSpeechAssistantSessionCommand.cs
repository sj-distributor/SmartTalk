using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantSessionCommand : ICommand
{
    public int AssistantId { get; set; }
}

public class AddAiSpeechAssistantSessionResponse : SmartTalkResponse<Guid>;