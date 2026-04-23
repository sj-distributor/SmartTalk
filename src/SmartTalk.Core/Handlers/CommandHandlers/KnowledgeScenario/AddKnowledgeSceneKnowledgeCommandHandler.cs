using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class AddKnowledgeSceneKnowledgeCommandHandler : ICommandHandler<AddKnowledgeSceneKnowledgeCommand, AddKnowledgeSceneKnowledgeResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public AddKnowledgeSceneKnowledgeCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<AddKnowledgeSceneKnowledgeResponse> Handle(IReceiveContext<AddKnowledgeSceneKnowledgeCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.AddKnowledgeSceneKnowledgeAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
