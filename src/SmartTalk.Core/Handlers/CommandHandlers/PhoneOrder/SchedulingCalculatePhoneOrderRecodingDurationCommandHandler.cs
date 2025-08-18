using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class SchedulingCalculatePhoneOrderRecodingDurationCommandHandler : ICommandHandler<SchedulingCalculatePhoneOrderRecodingDurationCommand>
{
    private readonly IPhoneOrderProcessJobService _phoneOrderProcessJobService;

    public SchedulingCalculatePhoneOrderRecodingDurationCommandHandler(IPhoneOrderProcessJobService phoneOrderProcessJobService)
    {
        _phoneOrderProcessJobService = phoneOrderProcessJobService;
    }

    public async Task Handle(IReceiveContext<SchedulingCalculatePhoneOrderRecodingDurationCommand> context, CancellationToken cancellationToken)
    {
        await _phoneOrderProcessJobService.CalculatePhoneOrderRecodingDurationAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}