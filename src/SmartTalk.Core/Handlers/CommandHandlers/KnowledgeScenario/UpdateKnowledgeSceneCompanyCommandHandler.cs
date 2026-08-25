using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class UpdateKnowledgeSceneCompanyCommandHandler : ICommandHandler<UpdateKnowledgeSceneCompanyCommand, UpdateKnowledgeSceneCompanyResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public UpdateKnowledgeSceneCompanyCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<UpdateKnowledgeSceneCompanyResponse> Handle(IReceiveContext<UpdateKnowledgeSceneCompanyCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.UpdateKnowledgeSceneCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
