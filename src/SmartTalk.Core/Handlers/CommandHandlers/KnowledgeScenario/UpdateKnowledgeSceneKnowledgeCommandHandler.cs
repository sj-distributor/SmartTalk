using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class UpdateKnowledgeSceneKnowledgeCommandHandler : ICommandHandler<UpdateKnowledgeSceneKnowledgeCommand, UpdateKnowledgeSceneKnowledgeResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public UpdateKnowledgeSceneKnowledgeCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<UpdateKnowledgeSceneKnowledgeResponse> Handle(IReceiveContext<UpdateKnowledgeSceneKnowledgeCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.UpdateKnowledgeSceneKnowledgeAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
