using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosStoreUsersRequestHandler : IRequestHandler<GetPosStoreUsersRequest, GetPosStoreUsersResponse>
{
    private readonly IPosService _service;

    public GetPosStoreUsersRequestHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<GetPosStoreUsersResponse> Handle(IReceiveContext<GetPosStoreUsersRequest> context, CancellationToken cancellationToken)
    {
        return await _service.GetPosStoreUsersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}