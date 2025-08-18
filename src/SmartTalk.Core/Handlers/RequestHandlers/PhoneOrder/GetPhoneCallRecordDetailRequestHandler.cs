using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneCallRecordDetailRequestHandler : IRequestHandler<GetPhoneCallRecordDetailRequest, GetPhoneCallRecordDetailResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneCallRecordDetailRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<GetPhoneCallRecordDetailResponse> Handle(IReceiveContext<GetPhoneCallRecordDetailRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneCallrecordDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}