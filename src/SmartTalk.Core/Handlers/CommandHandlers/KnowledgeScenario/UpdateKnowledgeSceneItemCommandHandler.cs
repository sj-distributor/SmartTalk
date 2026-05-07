using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class UpdateKnowledgeSceneItemCommandHandler : ICommandHandler<UpdateKnowledgeSceneItemCommand, UpdateKnowledgeSceneItemResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public UpdateKnowledgeSceneItemCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<UpdateKnowledgeSceneItemResponse> Handle(IReceiveContext<UpdateKnowledgeSceneItemCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.UpdateKnowledgeSceneItemAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
