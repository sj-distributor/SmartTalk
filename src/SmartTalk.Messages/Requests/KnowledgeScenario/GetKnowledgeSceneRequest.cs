using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeSceneRequest : IRequest
{
    public int Id { get; set; }
}

public class GetKnowledgeSceneResponse : SmartTalkResponse<GetKnowledgeSceneResponseData>
{
}

public class GetKnowledgeSceneResponseData
{
    public KnowledgeSceneDto SceneData { get; set; }
}