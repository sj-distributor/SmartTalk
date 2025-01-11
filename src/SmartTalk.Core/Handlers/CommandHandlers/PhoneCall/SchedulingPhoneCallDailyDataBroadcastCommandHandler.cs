using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneCall;
using SmartTalk.Messages.Commands.PhoneCall;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneCall;

public class SchedulingPhoneCallDailyDataBroadcastCommandHandler : ICommandHandler<SchedulingPhoneCallDailyDataBroadcastCommand>
{
    private readonly IPhoneCallService _phoneCallService;

    public SchedulingPhoneCallDailyDataBroadcastCommandHandler(IPhoneCallService phoneCallService)
    {
        _phoneCallService = phoneCallService;
    }

    public async Task Handle(IReceiveContext<SchedulingPhoneCallDailyDataBroadcastCommand> context, CancellationToken cancellationToken)
    {
        await _phoneCallService.DailyDataBroadcastAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}