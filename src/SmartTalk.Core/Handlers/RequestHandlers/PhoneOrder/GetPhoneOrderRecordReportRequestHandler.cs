using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneOrderRecordReportRequestHandler : IRequestHandler<GetPhoneOrderRecordReportRequest, GetPhoneOrderRecordReportResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderRecordReportRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<GetPhoneOrderRecordReportResponse> Handle(IReceiveContext<GetPhoneOrderRecordReportRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderRecordReportByCallSidAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}