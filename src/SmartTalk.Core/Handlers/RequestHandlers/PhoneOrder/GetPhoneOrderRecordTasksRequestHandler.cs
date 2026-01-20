using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneOrderRecordTasksRequestHandler : IRequestHandler<GetPhoneOrderRecordTasksRequest, GetPhoneOrderRecordTasksResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderRecordTasksRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<GetPhoneOrderRecordTasksResponse> Handle(IReceiveContext<GetPhoneOrderRecordTasksRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderRecordTasksRequestAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}