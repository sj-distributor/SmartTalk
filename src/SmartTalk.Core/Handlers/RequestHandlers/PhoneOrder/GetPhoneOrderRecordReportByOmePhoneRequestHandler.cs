using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneOrderRecordReportByOmePhoneRequestHandler : IRequestHandler<GetPhoneOrderRecordReportByOmePhoneRequest, GetPhoneOrderRecordReportByOmePhoneResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderRecordReportByOmePhoneRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<GetPhoneOrderRecordReportByOmePhoneResponse> Handle(IReceiveContext<GetPhoneOrderRecordReportByOmePhoneRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderRecordReportByOmePhoneAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}