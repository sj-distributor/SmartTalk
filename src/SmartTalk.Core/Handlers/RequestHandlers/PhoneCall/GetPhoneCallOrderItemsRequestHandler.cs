using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneCall;
using SmartTalk.Messages.Requests.PhoneCall;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneCallOrderItemsRequestHandler : IRequestHandler<GetPhoneCallOrderItemsRequest, GetPhoneCallOrderItemsRessponse>
{
    private readonly IPhoneCallService _phoneCallService;

    public GetPhoneCallOrderItemsRequestHandler(IPhoneCallService phoneCallService)
    {
        _phoneCallService = phoneCallService;
    }
    
    public async Task<GetPhoneCallOrderItemsRessponse> Handle(IReceiveContext<GetPhoneCallOrderItemsRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneCallService.GetPhoneOrderOrderItemsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}