using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class DeleteKnowledgeSceneFolderCommandHandler : ICommandHandler<DeleteKnowledgeSceneFolderCommand, DeleteKnowledgeSceneFolderResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public DeleteKnowledgeSceneFolderCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<DeleteKnowledgeSceneFolderResponse> Handle(IReceiveContext<DeleteKnowledgeSceneFolderCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.DeleteKnowledgeSceneFolderAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
