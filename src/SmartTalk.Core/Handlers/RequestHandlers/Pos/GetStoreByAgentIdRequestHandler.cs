using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetStoreByAgentIdRequestHandler : IRequestHandler<GetStoreByAgentIdRequest, GetStoreByAgentIdResponse>
{
    private readonly IPosService _posService;

    public GetStoreByAgentIdRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetStoreByAgentIdResponse> Handle(IReceiveContext<GetStoreByAgentIdRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetStoreByAgentIdAsync(context.Message, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}