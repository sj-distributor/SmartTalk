using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class SchedulingCalculatePhoneOrderRecodingDurationCommandHandler : ICommandHandler<SchedulingCalculatePhoneOrderRecodingDurationCommand>
{
    private readonly IPhoneOrderServiceProcessJobService _phoneOrderServiceProcessJobService;

    public SchedulingCalculatePhoneOrderRecodingDurationCommandHandler(IPhoneOrderServiceProcessJobService phoneOrderServiceProcessJobService)
    {
        _phoneOrderServiceProcessJobService = phoneOrderServiceProcessJobService;
    }

    public async Task Handle(IReceiveContext<SchedulingCalculatePhoneOrderRecodingDurationCommand> context, CancellationToken cancellationToken)
    {
        await _phoneOrderServiceProcessJobService.CalculatePhoneOrderRecodingDurationAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}