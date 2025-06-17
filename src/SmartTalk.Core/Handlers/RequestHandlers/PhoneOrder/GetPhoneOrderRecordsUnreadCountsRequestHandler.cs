using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneOrderRecordsUnreadCountsRequestHandler : IRequestHandler<GetPhoneOrderRecordsUnreadCountsRequest, GetPhoneOrderRecordsUnreadCountsResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderRecordsUnreadCountsRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<GetPhoneOrderRecordsUnreadCountsResponse> Handle(IReceiveContext<GetPhoneOrderRecordsUnreadCountsRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderRecordsUnreadCountsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}