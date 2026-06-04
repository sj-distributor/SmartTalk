using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Handlers.CommandHandlers.Sales;

public class SyncCrmSalesAutoCreateCommandHandler : ICommandHandler<SyncCrmSalesAutoCreateCommand, SyncCrmSalesAutoCreateResponse>
{
    private readonly ISalesAutoCreateService _salesAutoCreateService;

    public SyncCrmSalesAutoCreateCommandHandler(ISalesAutoCreateService salesAutoCreateService)
    {
        _salesAutoCreateService = salesAutoCreateService;
    }

    public async Task<SyncCrmSalesAutoCreateResponse> Handle(IReceiveContext<SyncCrmSalesAutoCreateCommand> context, CancellationToken cancellationToken)
    {
        return await _salesAutoCreateService.SyncCrmSalesAutoCreateAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
