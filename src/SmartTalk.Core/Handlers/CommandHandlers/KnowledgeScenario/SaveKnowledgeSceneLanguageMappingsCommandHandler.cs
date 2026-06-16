using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class SaveKnowledgeSceneLanguageMappingsCommandHandler : ICommandHandler<SaveKnowledgeSceneLanguageMappingsCommand, SaveKnowledgeSceneLanguageMappingsResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public SaveKnowledgeSceneLanguageMappingsCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<SaveKnowledgeSceneLanguageMappingsResponse> Handle(
        IReceiveContext<SaveKnowledgeSceneLanguageMappingsCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.SaveKnowledgeSceneLanguageMappingsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
