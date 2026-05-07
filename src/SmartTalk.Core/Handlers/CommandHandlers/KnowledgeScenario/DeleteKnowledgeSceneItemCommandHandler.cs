using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class DeleteKnowledgeSceneItemCommandHandler : ICommandHandler<DeleteKnowledgeSceneItemCommand, DeleteKnowledgeSceneItemResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public DeleteKnowledgeSceneItemCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<DeleteKnowledgeSceneItemResponse> Handle(IReceiveContext<DeleteKnowledgeSceneItemCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.DeleteKnowledgeSceneItemAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
