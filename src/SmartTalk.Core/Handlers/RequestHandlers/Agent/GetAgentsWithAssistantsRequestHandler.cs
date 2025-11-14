using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Handlers.RequestHandlers.Agent;

public class GetAgentsWithAssistantsRequestHandler : IRequestHandler<GetAgentsWithAssistantsRequest, GetAgentsWithAssistantsResponse>
{
    private readonly IAgentService _agentService;

    public GetAgentsWithAssistantsRequestHandler(IAgentService agentService)
    {
        _agentService = agentService;
    }

    public async Task<GetAgentsWithAssistantsResponse> Handle(IReceiveContext<GetAgentsWithAssistantsRequest> context, CancellationToken cancellationToken)
    {
        return await _agentService.GetAgentsWithAssistantsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}