using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class SwitchAiSpeechAssistantKnowledgeVersionCommand : ICommand
{
    public int AssistantId { get; set; }
    
    public int KnowledgeId { get; set; }
}

public class SwitchAiSpeechAssistantKnowledgeVersionResponse : SmartiesResponse<AiSpeechAssistantKnowledgeDto>
{
}