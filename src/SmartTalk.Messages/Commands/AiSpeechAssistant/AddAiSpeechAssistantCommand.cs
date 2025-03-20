using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantCommand : ICommand
{
    public string AssistantName { get; set; }
    
    public string Greetings { get; set; }
    
    public string Json { get; set; }
}

public class AddAiSpeechAssistantResponse : SmartiesResponse<AiSpeechAssistantDto>
{
}