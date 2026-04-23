using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeSceneKnowledgesRequest : IRequest
{
    public int SceneId { get; set; }

    public string Keyword { get; set; }
}

public class GetKnowledgeSceneKnowledgesResponse : SmartTalkResponse<List<KnowledgeSceneKnowledgeDto>>
{
}
