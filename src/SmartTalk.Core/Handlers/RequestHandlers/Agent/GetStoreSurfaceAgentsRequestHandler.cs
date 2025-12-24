using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Handlers.RequestHandlers.Agent;

public class GetStoreSurfaceAgentsRequestHandler : IRequestHandler<GetStoreSurfaceAgentsRequest, GetStoreSurfaceAgentsResponse>
{
    private readonly IAgentService _agentService;

    public GetStoreSurfaceAgentsRequestHandler(IAgentService agentService)
    {
        _agentService = agentService;
    }

    public async Task<GetStoreSurfaceAgentsResponse> Handle(IReceiveContext<GetStoreSurfaceAgentsRequest> context, CancellationToken cancellationToken)
    {
        return await _agentService.GetStoreSurfaceAgentsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}