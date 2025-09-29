using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantKnowledgeCommand : ICommand
{
    public int AssistantId { get; set; }
    
    public string Greetings { get; set; }
    
    public string Json { get; set; }
    
    public AiSpeechAssistantMainLanguage? Language { get; set; }
}

public class AddAiSpeechAssistantKnowledgeResponse : SmartTalkResponse<AiSpeechAssistantKnowledgeDto>
{
}