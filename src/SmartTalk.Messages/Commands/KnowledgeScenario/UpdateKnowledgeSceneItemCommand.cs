using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class UpdateKnowledgeSceneItemCommand : ICommand
{
    public int Id { get; set; }

    public string Name { get; set; }

    public KnowledgeSceneItemType Type { get; set; }

    public string Content { get; set; }

    public string FileName { get; set; }
}

public class UpdateKnowledgeSceneItemResponse : SmartTalkResponse<KnowledgeSceneItemDto>
{
}
