using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.KnowledgeScenario;

public class DeleteKnowledgeSceneKnowledgeCommand : ICommand
{
    public int Id { get; set; }
}

public class DeleteKnowledgeSceneKnowledgeResponse : SmartTalkResponse<KnowledgeSceneKnowledgeDto>
{
}
