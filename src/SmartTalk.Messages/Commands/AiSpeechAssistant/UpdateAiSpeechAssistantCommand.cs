using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantCommand : ICommand
{
    public int AssistantId { get; set; }
    
    public string AssistantName { get; set; }
    
    public int? AnsweringNumberId { get; set; }
    
    public string AnsweringNumber { get; set; }
}

public class UpdateAiSpeechAssistantResponse : SmartTalkResponse<AiSpeechAssistantDto>
{
}