using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosCustomerInfoRequestHandler : IRequestHandler<GetStoreCustomersRequest, GetStoreCustomersResponse>
{
    private readonly IPosService _posService;

    public GetPosCustomerInfoRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetStoreCustomersResponse> Handle(IReceiveContext<GetStoreCustomersRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetStoreCustomersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}