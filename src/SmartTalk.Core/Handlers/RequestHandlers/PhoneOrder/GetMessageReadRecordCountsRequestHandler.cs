using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetMessageReadRecordCountsRequestHandler : IRequestHandler<GetMessageReadRecordCountsRequest, GetMessageReadRecordCountsResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetMessageReadRecordCountsRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<GetMessageReadRecordCountsResponse> Handle(IReceiveContext<GetMessageReadRecordCountsRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetMessageReadRecordCountsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}