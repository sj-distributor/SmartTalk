using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeSceneRelationsRequest : IRequest
{
    public int KnowledgeId { get; set; }
}

public class GetAiSpeechAssistantKnowledgeSceneRelationsResponse : SmartTalkResponse<List<AiSpeechAssistantKnowledgeSceneRelationDto>>
{
}
