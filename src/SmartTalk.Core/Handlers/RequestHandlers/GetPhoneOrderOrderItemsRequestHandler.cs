using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests;

namespace SmartTalk.Core.Handlers.RequestHandlers;

public class GetPhoneOrderOrderItemsRequestHandler : IRequestHandler<GetPhoneOrderOrderItemsRequest, GetPhoneOrderOrderItemsRessponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderOrderItemsRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<GetPhoneOrderOrderItemsRessponse> Handle(IReceiveContext<GetPhoneOrderOrderItemsRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderOrderItemsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}