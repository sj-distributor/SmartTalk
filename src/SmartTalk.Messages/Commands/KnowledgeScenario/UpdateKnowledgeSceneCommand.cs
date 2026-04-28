using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class UpdateKnowledgeSceneCommand : ICommand
{
    public int Id { get; set; }

    public int FolderId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public KnowledgeSceneStatus Status { get; set; }
}

public class UpdateKnowledgeSceneResponse : SmartTalkResponse<KnowledgeSceneDto>
{
}
