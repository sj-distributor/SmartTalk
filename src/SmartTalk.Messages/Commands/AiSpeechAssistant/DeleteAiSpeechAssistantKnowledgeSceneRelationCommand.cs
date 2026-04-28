using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class DeleteAiSpeechAssistantKnowledgeSceneRelationCommand : ICommand
{
    public int Id { get; set; }
}

public class DeleteAiSpeechAssistantKnowledgeSceneRelationResponse : SmartTalkResponse<AiSpeechAssistantKnowledgeSceneRelationDto>
{
}
