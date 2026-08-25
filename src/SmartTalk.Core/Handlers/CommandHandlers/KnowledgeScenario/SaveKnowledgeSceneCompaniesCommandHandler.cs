using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class SaveKnowledgeSceneCompaniesCommandHandler : ICommandHandler<SaveKnowledgeSceneCompaniesCommand, SaveKnowledgeSceneCompaniesResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public SaveKnowledgeSceneCompaniesCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<SaveKnowledgeSceneCompaniesResponse> Handle(IReceiveContext<SaveKnowledgeSceneCompaniesCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.SaveKnowledgeSceneCompaniesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
