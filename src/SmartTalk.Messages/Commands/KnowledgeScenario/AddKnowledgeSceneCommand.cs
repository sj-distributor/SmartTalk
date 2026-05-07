using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class AddKnowledgeSceneCommand : ICommand
{
    public int FolderId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public KnowledgeSceneStatus Status { get; set; } = KnowledgeSceneStatus.OffShelf;
}

public class AddKnowledgeSceneResponse : SmartTalkResponse<KnowledgeSceneDto>
{
}
