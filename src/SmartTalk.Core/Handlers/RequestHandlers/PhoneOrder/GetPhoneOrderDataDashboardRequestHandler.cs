using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneOrderDataDashboardRequestHandler : IRequestHandler<GetPhoneOrderDataDashboardRequest, GetPhoneOrderDataDashboardResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderDataDashboardRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<GetPhoneOrderDataDashboardResponse> Handle(IReceiveContext<GetPhoneOrderDataDashboardRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderDataDashboardAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}