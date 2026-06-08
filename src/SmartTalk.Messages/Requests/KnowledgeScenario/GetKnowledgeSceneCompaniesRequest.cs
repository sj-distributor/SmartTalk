using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeSceneCompaniesRequest : IRequest
{
    public int SceneId { get; set; }
}

public class GetKnowledgeSceneCompaniesResponse : SmartTalkResponse<List<KnowledgeSceneCompanyDto>>
{
}
