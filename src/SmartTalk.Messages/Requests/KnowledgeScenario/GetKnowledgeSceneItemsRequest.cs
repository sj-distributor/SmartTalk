using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeSceneItemsRequest : IRequest
{
    public int SceneId { get; set; }

    public string Keyword { get; set; }
}

public class GetKnowledgeSceneItemsResponse : SmartTalkResponse<List<KnowledgeSceneItemDto>>
{
}
