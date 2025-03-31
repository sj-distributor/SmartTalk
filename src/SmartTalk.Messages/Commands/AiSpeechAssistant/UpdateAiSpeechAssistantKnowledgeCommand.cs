using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantKnowledgeCommand : ICommand
{
    public string Brief { get; set; }
    
    public int KnowledgeId { get; set; }
}

public class UpdateAiSpeechAssistantKnowledgeResponse : SmartiesResponse<AiSpeechAssistantKnowledgeDto>
{
}