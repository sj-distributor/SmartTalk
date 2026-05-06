using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class SaveKnowledgeSceneRelatedKnowledgesCommand : ICommand
{
    public int SceneId { get; set; }

    public List<int> KnowledgeIds { get; set; }
}

public class SaveKnowledgeSceneRelatedKnowledgesResponse : SmartTalkResponse<List<int>>
{
}
