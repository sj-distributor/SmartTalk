using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Commands.Agent;

namespace SmartTalk.Core.Handlers.CommandHandlers.Agent;

public class AddAgentCommandHandler : ICommandHandler<AddAgentCommand, AddAgentResponse>
{
    private readonly IAgentService _agentService;

    public AddAgentCommandHandler(IAgentService agentService)
    {
        _agentService = agentService;
    }

    public async Task<AddAgentResponse> Handle(IReceiveContext<AddAgentCommand> context, CancellationToken cancellationToken)
    {
        return await _agentService.AddAgentAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}