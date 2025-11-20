using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Handlers.CommandHandlers.Sales;

public class RefreshAllCustomerItemsCacheCommandHandler : ICommandHandler<RefreshAllCustomerItemsCacheCommand>
{
    private readonly ISalesJobProcessJobService _salesJobProcessJobService;

    public RefreshAllCustomerItemsCacheCommandHandler(ISalesJobProcessJobService salesJobProcessJobService)
    {
        _salesJobProcessJobService = salesJobProcessJobService;
    }

    public async Task Handle(IReceiveContext<RefreshAllCustomerItemsCacheCommand> context, CancellationToken cancellationToken)
    {
        await _salesJobProcessJobService.ScheduleRefreshCustomerItemsCacheAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}