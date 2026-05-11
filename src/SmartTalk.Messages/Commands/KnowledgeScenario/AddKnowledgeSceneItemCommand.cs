using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class AddKnowledgeSceneItemCommand : ICommand
{
    public int SceneId { get; set; }

    public List<AddKnowledgeSceneItemDto> Items { get; set; } = new();
}

public class AddKnowledgeSceneItemResponse : SmartTalkResponse<List<KnowledgeSceneItemDto>>
{
}
