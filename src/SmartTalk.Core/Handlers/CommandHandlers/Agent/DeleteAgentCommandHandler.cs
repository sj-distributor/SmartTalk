using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Commands.Agent;

namespace SmartTalk.Core.Handlers.CommandHandlers.Agent;

public class DeleteAgentCommandHandler : ICommandHandler<DeleteAgentCommand, DeleteAgentResponse>
{
    private readonly IAgentService _agentService;

    public DeleteAgentCommandHandler(IAgentService agentService)
    {
        _agentService = agentService;
    }

    public async Task<DeleteAgentResponse> Handle(IReceiveContext<DeleteAgentCommand> context, CancellationToken cancellationToken)
    {
        return await _agentService.DeleteAgentAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}