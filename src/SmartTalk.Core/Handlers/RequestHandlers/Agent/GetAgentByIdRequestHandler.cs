using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Handlers.RequestHandlers.Agent;

public class GetAgentByIdRequestHandler : IRequestHandler<GetAgentByIdRequest, GetAgentByIdResponse>
{
    private readonly IAgentService _agentService;

    public GetAgentByIdRequestHandler(IAgentService agentService)
    {
        _agentService = agentService;
    }

    public async Task<GetAgentByIdResponse> Handle(IReceiveContext<GetAgentByIdRequest> context, CancellationToken cancellationToken)
    {
        return await _agentService.GetAgentByIdAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}