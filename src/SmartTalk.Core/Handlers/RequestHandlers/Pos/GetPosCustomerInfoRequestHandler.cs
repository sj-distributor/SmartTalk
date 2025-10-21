using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosCustomerInfoRequestHandler : IRequestHandler<GetPosCustomerInfoRequest, GetPosCustomerInfoResponse>
{
    private readonly IPosService _posService;

    public GetPosCustomerInfoRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetPosCustomerInfoResponse> Handle(IReceiveContext<GetPosCustomerInfoRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosCustomerInfosAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}