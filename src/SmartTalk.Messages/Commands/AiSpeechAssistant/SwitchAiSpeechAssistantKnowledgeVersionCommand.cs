using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class SwitchAiSpeechAssistantKnowledgeVersionCommand : ICommand
{
    public int AssistantId { get; set; }
    
    public int KnowledgeId { get; set; }
}

public class SwitchAiSpeechAssistantKnowledgeVersionResponse : SmartTalkResponse<AiSpeechAssistantKnowledgeDto>
{
}