using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class UpdateKnowledgeSceneCommandHandler : ICommandHandler<UpdateKnowledgeSceneCommand, UpdateKnowledgeSceneResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public UpdateKnowledgeSceneCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<UpdateKnowledgeSceneResponse> Handle(IReceiveContext<UpdateKnowledgeSceneCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.UpdateKnowledgeSceneAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
