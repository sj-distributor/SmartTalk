using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class DeleteKnowledgeSceneKnowledgeCommandHandler : ICommandHandler<DeleteKnowledgeSceneKnowledgeCommand, DeleteKnowledgeSceneKnowledgeResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public DeleteKnowledgeSceneKnowledgeCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<DeleteKnowledgeSceneKnowledgeResponse> Handle(IReceiveContext<DeleteKnowledgeSceneKnowledgeCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.DeleteKnowledgeSceneKnowledgeAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
