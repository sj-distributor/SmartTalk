using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantKnowledgeCommand : ICommand
{
    public int AssistantId { get; set; }
    
    public string Greetings { get; set; }
    
    public string Json { get; set; }
}

public class AddAiSpeechAssistantKnowledgeResponse : SmartiesResponse<AiSpeechAssistantKnowledgeDto>
{
}