using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class AddKnowledgeSceneCommandHandler : ICommandHandler<AddKnowledgeSceneCommand, AddKnowledgeSceneResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public AddKnowledgeSceneCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<AddKnowledgeSceneResponse> Handle(IReceiveContext<AddKnowledgeSceneCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.AddKnowledgeSceneAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
