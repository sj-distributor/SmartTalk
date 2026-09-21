using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class SaveKnowledgeSceneLanguageMappingsCommand : ICommand
{
    public int CompanyId { get; set; }

    public List<SaveKnowledgeSceneLanguageMappingItemDto> Mappings { get; set; } = new();
}

public class SaveKnowledgeSceneLanguageMappingsResponse : SmartTalkResponse<KnowledgeSceneAutoAddLanguageMappingsDto>
{
}
