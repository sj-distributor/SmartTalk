using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Commands.Agent;

namespace SmartTalk.Core.Handlers.CommandHandlers.Agent;

public class UpdateAgentCommandHandler : ICommandHandler<UpdateAgentCommand, UpdateAgentResponse>
{
    private readonly IAgentService _agentService;

    public UpdateAgentCommandHandler(IAgentService agentService)
    {
        _agentService = agentService;
    }

    public async Task<UpdateAgentResponse> Handle(IReceiveContext<UpdateAgentCommand> context, CancellationToken cancellationToken)
    {
        return await _agentService.UpdateAgentAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}