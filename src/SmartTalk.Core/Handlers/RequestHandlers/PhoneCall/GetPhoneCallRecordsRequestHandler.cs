using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneCall;
using SmartTalk.Messages.Requests.PhoneCall;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneCallRecordsRequestHandler : IRequestHandler<GetPhoneCallRecordsRequest, GetPhoneCallRecordsResponse >
{
    private readonly IPhoneCallService _phoneCallService;

    public GetPhoneCallRecordsRequestHandler(IPhoneCallService phoneCallService)
    {
        _phoneCallService = phoneCallService;
    }

    public async Task<GetPhoneCallRecordsResponse> Handle(IReceiveContext<GetPhoneCallRecordsRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneCallService.GetPhoneOrderRecordsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}