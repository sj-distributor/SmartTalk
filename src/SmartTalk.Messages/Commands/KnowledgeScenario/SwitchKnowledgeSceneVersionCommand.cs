using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class SwitchKnowledgeSceneVersionCommand : ICommand
{
    public int SceneId { get; set; }

    public int HistoryId { get; set; }
}

public class SwitchKnowledgeSceneVersionResponse : SmartTalkResponse<KnowledgeSceneDto>
{
}
