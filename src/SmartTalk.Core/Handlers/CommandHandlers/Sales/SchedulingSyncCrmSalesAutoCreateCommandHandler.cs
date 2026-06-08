using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Handlers.CommandHandlers.Sales;

public class SchedulingSyncCrmSalesAutoCreateCommandHandler : ICommandHandler<SchedulingSyncCrmSalesAutoCreateCommand>
{
    private readonly ISalesAutoCreateService _salesAutoCreateService;
    private readonly SalesAutoCreateSetting _salesAutoCreateSetting;

    public SchedulingSyncCrmSalesAutoCreateCommandHandler(
        ISalesAutoCreateService salesAutoCreateService,
        SalesAutoCreateSetting salesAutoCreateSetting)
    {
        _salesAutoCreateService = salesAutoCreateService;
        _salesAutoCreateSetting = salesAutoCreateSetting;
    }

    public async Task Handle(IReceiveContext<SchedulingSyncCrmSalesAutoCreateCommand> context, CancellationToken cancellationToken)
    {
        await _salesAutoCreateService.SyncCrmSalesAutoCreateAsync(new SyncCrmSalesAutoCreateCommand
        {
            ServiceProviderId = _salesAutoCreateSetting.ServiceProviderId
        }, cancellationToken).ConfigureAwait(false);
    }
}
