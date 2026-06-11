using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class AddKnowledgeSceneFolderCommandHandler : ICommandHandler<AddKnowledgeSceneFolderCommand, AddKnowledgeSceneFolderResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public AddKnowledgeSceneFolderCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<AddKnowledgeSceneFolderResponse> Handle(IReceiveContext<AddKnowledgeSceneFolderCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.AddKnowledgeSceneFolderAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
