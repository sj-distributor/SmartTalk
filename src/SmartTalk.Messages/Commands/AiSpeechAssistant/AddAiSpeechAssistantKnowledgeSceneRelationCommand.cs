using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantKnowledgeSceneRelationCommand : ICommand
{
    public int KnowledgeId { get; set; }

    public int SceneId { get; set; }
}

public class AddAiSpeechAssistantKnowledgeSceneRelationResponse : SmartTalkResponse<AiSpeechAssistantKnowledgeSceneRelationDto>
{
}
