using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneOrderOrderItemsRequestHandler : IRequestHandler<GetPhoneOrderOrderItemsRequest, GetPhoneOrderOrderItemsResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderOrderItemsRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<GetPhoneOrderOrderItemsResponse> Handle(IReceiveContext<GetPhoneOrderOrderItemsRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderOrderItemsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}