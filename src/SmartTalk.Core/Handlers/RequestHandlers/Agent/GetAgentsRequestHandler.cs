using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Handlers.RequestHandlers.Agent;

public class GetAgentsRequestHandler : IRequestHandler<GetAgentsRequest, GetAgentsResponse>
{
    private readonly IAgentService _agentService;

    public GetAgentsRequestHandler(IAgentService agentService)
    {
        _agentService = agentService;
    }
    
    public async Task<GetAgentsResponse> Handle(IReceiveContext<GetAgentsRequest> context, CancellationToken cancellationToken)
    {
        return await _agentService.GetAgentsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}