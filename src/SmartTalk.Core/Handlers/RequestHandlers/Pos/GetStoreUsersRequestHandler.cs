using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetStoreUsersRequestHandler : IRequestHandler<GetStoreUsersRequest, GetStoreUsersResponse>
{
    private readonly IPosService _service;

    public GetStoreUsersRequestHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<GetStoreUsersResponse> Handle(IReceiveContext<GetStoreUsersRequest> context, CancellationToken cancellationToken)
    {
        return await _service.GetStoreUsersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}