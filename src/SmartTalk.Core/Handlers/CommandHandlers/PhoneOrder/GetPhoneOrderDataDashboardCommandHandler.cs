using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class GetPhoneOrderDataDashboardCommandHandler : ICommandHandler<GetPhoneOrderDataDashboardCommand, GetPhoneOrderDataDashboardResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderDataDashboardCommandHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<GetPhoneOrderDataDashboardResponse> Handle(IReceiveContext<GetPhoneOrderDataDashboardCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderDataDashboardAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
