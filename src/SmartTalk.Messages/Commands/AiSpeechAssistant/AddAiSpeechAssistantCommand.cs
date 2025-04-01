using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantCommand : ICommand
{
    public string AssistantName { get; set; }
    
    public string Greetings { get; set; }
    
    public string Json { get; set; }
}

public class AddAiSpeechAssistantResponse : SmartTalkResponse<AiSpeechAssistantDto>
{
}