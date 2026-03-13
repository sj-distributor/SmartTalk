using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneOrderCompanyCallReportRequestHandler : IRequestHandler<GetPhoneOrderCompanyCallReportRequest, GetPhoneOrderCompanyCallReportResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderCompanyCallReportRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<GetPhoneOrderCompanyCallReportResponse> Handle(IReceiveContext<GetPhoneOrderCompanyCallReportRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderCompanyCallReportAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
