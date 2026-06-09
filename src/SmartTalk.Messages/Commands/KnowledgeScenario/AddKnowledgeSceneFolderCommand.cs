using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class AddKnowledgeSceneFolderCommand : ICommand
{
    public string Name { get; set; }
}

public class AddKnowledgeSceneFolderResponse : SmartTalkResponse<KnowledgeSceneFolderDto>
{
}
