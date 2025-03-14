using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantCommand : ICommand
{
    public int AgentId { get; set; }
    
    public string AssistantName { get; set; }
    
    public int AnsweringNumberId { get; set; }
    
    public string AnsweringNumber { get; set; }
}

public class AddAiSpeechAssistantResponse : SmartiesResponse<AiSpeechAssistantDto>
{
}