using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class AddKnowledgeSceneItemCommandHandler : ICommandHandler<AddKnowledgeSceneItemCommand, AddKnowledgeSceneItemResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public AddKnowledgeSceneItemCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<AddKnowledgeSceneItemResponse> Handle(IReceiveContext<AddKnowledgeSceneItemCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.AddKnowledgeSceneItemAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
