using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class DeleteKnowledgeSceneCommandHandler : ICommandHandler<DeleteKnowledgeSceneCommand, DeleteKnowledgeSceneResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public DeleteKnowledgeSceneCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<DeleteKnowledgeSceneResponse> Handle(IReceiveContext<DeleteKnowledgeSceneCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.DeleteKnowledgeSceneAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
