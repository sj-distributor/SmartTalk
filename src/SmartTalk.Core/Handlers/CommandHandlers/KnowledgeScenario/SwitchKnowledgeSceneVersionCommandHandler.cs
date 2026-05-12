using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.CommandHandlers.KnowledgeScenario;

public class SwitchKnowledgeSceneVersionCommandHandler : ICommandHandler<SwitchKnowledgeSceneVersionCommand, SwitchKnowledgeSceneVersionResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public SwitchKnowledgeSceneVersionCommandHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<SwitchKnowledgeSceneVersionResponse> Handle(IReceiveContext<SwitchKnowledgeSceneVersionCommand> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.SwitchKnowledgeSceneVersionAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
