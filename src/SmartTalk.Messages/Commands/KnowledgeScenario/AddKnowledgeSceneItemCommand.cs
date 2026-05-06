using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class AddKnowledgeSceneItemCommand : ICommand
{
    public int SceneId { get; set; }

    public string Name { get; set; }

    public KnowledgeSceneItemType Type { get; set; } = KnowledgeSceneItemType.Text;

    public string Content { get; set; }

    public string FileName { get; set; }
}

public class AddKnowledgeSceneItemResponse : SmartTalkResponse<KnowledgeSceneItemDto>
{
}
