using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class UpdateKnowledgeSceneFolderCommand : ICommand
{
    public int Id { get; set; }

    public string Name { get; set; }
}

public class UpdateKnowledgeSceneFolderResponse : SmartTalkResponse<KnowledgeSceneFolderDto>
{
}
