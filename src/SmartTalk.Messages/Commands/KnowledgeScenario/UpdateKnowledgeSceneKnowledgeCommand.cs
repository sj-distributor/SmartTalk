using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class UpdateKnowledgeSceneKnowledgeCommand : ICommand
{
    public int Id { get; set; }

    public string Name { get; set; }

    public KnowledgeSceneKnowledgeType Type { get; set; }

    public string Content { get; set; }

    public string FileName { get; set; }
}

public class UpdateKnowledgeSceneKnowledgeResponse : SmartTalkResponse<KnowledgeSceneKnowledgeDto>
{
}
