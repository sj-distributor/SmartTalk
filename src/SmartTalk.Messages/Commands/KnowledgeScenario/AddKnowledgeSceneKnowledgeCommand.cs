using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class AddKnowledgeSceneKnowledgeCommand : ICommand
{
    public int SceneId { get; set; }

    public string Name { get; set; }

    public KnowledgeSceneKnowledgeType Type { get; set; } = KnowledgeSceneKnowledgeType.Text;

    public string Content { get; set; }

    public string FileName { get; set; }
}

public class AddKnowledgeSceneKnowledgeResponse : SmartTalkResponse<KnowledgeSceneKnowledgeDto>
{
}
