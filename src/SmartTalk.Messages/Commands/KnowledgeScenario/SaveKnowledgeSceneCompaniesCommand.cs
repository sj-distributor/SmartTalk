using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class SaveKnowledgeSceneCompaniesCommand : ICommand
{
    public int SceneId { get; set; }

    public List<int> CompanyIds { get; set; } = [];
}

public class SaveKnowledgeSceneCompaniesResponse : SmartTalkResponse<List<KnowledgeSceneCompanyDto>>
{
}
