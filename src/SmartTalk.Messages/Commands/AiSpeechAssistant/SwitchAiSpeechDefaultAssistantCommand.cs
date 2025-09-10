using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class SwitchAiSpeechDefaultAssistantCommand : ICommand
{
    public int PreviousAssistantId { get; set; }
    
    public int LatestAssistantId { get; set; }
}

public class SwitchAiSpeechDefaultAssistantResponse : SmartTalkResponse<AiSpeechAssistantDto>
{
}