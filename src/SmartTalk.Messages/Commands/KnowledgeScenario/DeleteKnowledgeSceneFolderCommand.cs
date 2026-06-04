using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class DeleteKnowledgeSceneFolderCommand : ICommand
{
    public int Id { get; set; }
}

public class DeleteKnowledgeSceneFolderResponse : SmartTalkResponse<KnowledgeSceneFolderDto>
{
}
