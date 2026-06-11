using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class UpdateKnowledgeSceneFolderCommandHandler : ICommandHandler<UpdateKnowledgeSceneFolderCommand, UpdateKnowledgeSceneFolderResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public UpdateKnowledgeSceneFolderCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<UpdateKnowledgeSceneFolderResponse> Handle(IReceiveContext<UpdateKnowledgeSceneFolderCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.UpdateKnowledgeSceneFolderAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
