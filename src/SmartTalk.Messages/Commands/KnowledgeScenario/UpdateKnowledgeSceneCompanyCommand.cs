using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class UpdateKnowledgeSceneCompanyCommand : ICommand
{
    public int SceneId { get; set; }

    public int CompanyId { get; set; }

    public bool IsApplied { get; set; }
}

public class UpdateKnowledgeSceneCompanyResponse : SmartTalkResponse<KnowledgeSceneCompanyDto>
{
}
