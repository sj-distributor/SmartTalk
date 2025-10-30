using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Handlers.RequestHandlers.Agent;

public class GetSurfaceAgentsRequestHandler : IRequestHandler<GetSurfaceAgentsRequest, GetSurfaceAgentsResponse>
{
    private readonly IAgentService _agentService;

    public GetSurfaceAgentsRequestHandler(IAgentService agentService)
    {
        _agentService = agentService;
    }

    public async Task<GetSurfaceAgentsResponse> Handle(IReceiveContext<GetSurfaceAgentsRequest> context, CancellationToken cancellationToken)
    {
        return await _agentService.GetSurfaceAgentsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}