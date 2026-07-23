using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeSceneLanguageMappingsRequest : IRequest
{
    public int CompanyId { get; set; }
}

public class GetKnowledgeSceneLanguageMappingsResponse : SmartTalkResponse<KnowledgeSceneAutoAddLanguageMappingsDto>
{
}
