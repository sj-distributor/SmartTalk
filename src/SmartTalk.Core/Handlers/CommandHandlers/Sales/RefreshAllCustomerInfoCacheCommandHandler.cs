using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Handlers.CommandHandlers.Sales;

public class RefreshAllCustomerInfoCacheCommandHandler : ICommandHandler<RefreshAllCustomerInfoCacheCommand>
{
    private readonly ISalesJobProcessJobService _salesJobProcessJobService;

    public RefreshAllCustomerInfoCacheCommandHandler(ISalesJobProcessJobService salesJobProcessJobService)
    {
        _salesJobProcessJobService = salesJobProcessJobService;
    }

    public async Task Handle(IReceiveContext<RefreshAllCustomerInfoCacheCommand> context, CancellationToken cancellationToken)
    {
        await _salesJobProcessJobService.ScheduleRefreshCrmCustomerInfoAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}