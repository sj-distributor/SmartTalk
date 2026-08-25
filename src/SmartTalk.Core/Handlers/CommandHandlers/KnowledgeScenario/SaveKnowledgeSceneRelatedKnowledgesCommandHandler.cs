using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class SaveKnowledgeSceneRelatedKnowledgesCommandHandler : ICommandHandler<SaveKnowledgeSceneRelatedKnowledgesCommand, SaveKnowledgeSceneRelatedKnowledgesResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public SaveKnowledgeSceneRelatedKnowledgesCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<SaveKnowledgeSceneRelatedKnowledgesResponse> Handle(IReceiveContext<SaveKnowledgeSceneRelatedKnowledgesCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.SaveKnowledgeSceneRelatedKnowledgesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
