using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class UpdateKnowledgeSceneHistoryCommand : ICommand
{
    public int SceneId { get; set; }

    public int HistoryId { get; set; }

    public string Brief { get; set; }
}

public class UpdateKnowledgeSceneHistoryResponse : SmartTalkResponse<KnowledgeSceneHistoryDto>
{
}
