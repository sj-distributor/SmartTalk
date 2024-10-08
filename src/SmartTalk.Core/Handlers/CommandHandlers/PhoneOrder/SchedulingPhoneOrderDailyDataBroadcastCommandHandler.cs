using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class SchedulingPhoneOrderDailyDataBroadcastCommandHandler : ICommandHandler<SchedulingPhoneOrderDailyDataBroadcastCommand>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public SchedulingPhoneOrderDailyDataBroadcastCommandHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task Handle(IReceiveContext<SchedulingPhoneOrderDailyDataBroadcastCommand> context, CancellationToken cancellationToken)
    {
        await _phoneOrderService.DailyDataBroadcastAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}