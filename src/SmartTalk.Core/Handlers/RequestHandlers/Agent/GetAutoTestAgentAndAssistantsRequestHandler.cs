using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Handlers.RequestHandlers.Agent;

public class GetAutoTestAgentAndAssistantsRequestHandler : IRequestHandler<GetAutoTestAgentAndAssistantsRequest, GetAutoTestAgentAndAssistantsResponse>
{
    private readonly IAgentService _agentService;

    public GetAutoTestAgentAndAssistantsRequestHandler(IAgentService agentService)
    {
        _agentService = agentService;
    }
    
    public async Task<GetAutoTestAgentAndAssistantsResponse> Handle(IReceiveContext<GetAutoTestAgentAndAssistantsRequest> context, CancellationToken cancellationToken)
    {
        return await _agentService.GetAutoTestAgentAndAssistantsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}