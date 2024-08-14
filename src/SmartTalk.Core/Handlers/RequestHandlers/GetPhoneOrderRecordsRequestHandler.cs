using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests;

namespace SmartTalk.Core.Handlers.RequestHandlers;

public class GetPhoneOrderRecordsRequestHandler : IRequestHandler<GetPhoneOrderRecordsRequest, GetPhoneOrderRecordsResponse >
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderRecordsRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<GetPhoneOrderRecordsResponse> Handle(IReceiveContext<GetPhoneOrderRecordsRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderRecordsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}