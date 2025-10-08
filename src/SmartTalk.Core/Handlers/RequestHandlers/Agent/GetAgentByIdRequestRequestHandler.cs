using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Handlers.RequestHandlers.Agent;

public class GetAgentByIdRequestRequestHandler : IRequestHandler<GetAgentByIdRequest, GetAgentByIdResponse>
{
    private readonly IAgentService _agentService;

    public GetAgentByIdRequestRequestHandler(IAgentService agentService)
    {
        _agentService = agentService;
    }

    public async Task<GetAgentByIdResponse> Handle(IReceiveContext<GetAgentByIdRequest> context, CancellationToken cancellationToken)
    {
        return await _agentService.GetAgentByIdAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}