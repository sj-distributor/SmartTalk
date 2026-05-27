using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeSceneRelatedKnowledgesRequest : IRequest
{
    public int SceneId { get; set; }
}

public class GetKnowledgeSceneRelatedKnowledgesResponse : SmartTalkResponse<List<int>>
{
}
