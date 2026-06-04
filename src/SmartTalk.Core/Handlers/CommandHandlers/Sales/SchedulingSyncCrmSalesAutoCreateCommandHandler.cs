using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Handlers.CommandHandlers.Sales;

public class SchedulingSyncCrmSalesAutoCreateCommandHandler : ICommandHandler<SchedulingSyncCrmSalesAutoCreateCommand>
{
    private readonly ISalesAutoCreateService _salesAutoCreateService;

    public SchedulingSyncCrmSalesAutoCreateCommandHandler(ISalesAutoCreateService salesAutoCreateService)
    {
        _salesAutoCreateService = salesAutoCreateService;
    }

    public async Task Handle(IReceiveContext<SchedulingSyncCrmSalesAutoCreateCommand> context, CancellationToken cancellationToken)
    {
        await _salesAutoCreateService.SyncCrmSalesAutoCreateAsync(new SyncCrmSalesAutoCreateCommand(), cancellationToken).ConfigureAwait(false);
    }
}
