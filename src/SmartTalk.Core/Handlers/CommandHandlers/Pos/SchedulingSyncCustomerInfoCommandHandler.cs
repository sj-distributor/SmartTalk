using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class SchedulingSyncCustomerInfoCommandHandler : ICommandHandler<SchedulingSyncCustomerInfoCommand>
{
    private readonly IPosProcessJobService _posProcessJobService;

    public SchedulingSyncCustomerInfoCommandHandler(IPosProcessJobService posProcessJobService)
    {
        _posProcessJobService = posProcessJobService;
    }

    public async Task Handle(IReceiveContext<SchedulingSyncCustomerInfoCommand> context, CancellationToken cancellationToken)
    {
        await _posProcessJobService.SyncCustomerInfoFromOrderAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}