using Mediator.Net.Context;
using Mediator.Net.Contracts;
using Serilog;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Handlers.CommandHandlers.Sales;

public class SchedulingSyncCrmSalesAutoCreateCommandHandler : ICommandHandler<SchedulingSyncCrmSalesAutoCreateCommand>
{
    private readonly ISalesAutoCreateService _salesAutoCreateService;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly SalesAutoCreateSetting _salesAutoCreateSetting;

    public SchedulingSyncCrmSalesAutoCreateCommandHandler(
        ISalesAutoCreateService salesAutoCreateService,
        ISalesDataProvider salesDataProvider,
        SalesAutoCreateSetting salesAutoCreateSetting)
    {
        _salesAutoCreateService = salesAutoCreateService;
        _salesDataProvider = salesDataProvider;
        _salesAutoCreateSetting = salesAutoCreateSetting;
    }

    public async Task Handle(IReceiveContext<SchedulingSyncCrmSalesAutoCreateCommand> context, CancellationToken cancellationToken)
    {
        var latestInitialRelease = await _salesDataProvider
            .GetLatestSuccessfulCrmSalesAutoSyncRunByModeAsync("initial", cancellationToken)
            .ConfigureAwait(false);

        if (SalesAutoCreateService.ShouldSkipAutomaticSyncForInitialReleaseDay(latestInitialRelease?.CreatedDate, DateTimeOffset.UtcNow))
        {
            Log.Information(
                "Skip scheduled CRM sales auto-create sync because initial release already completed on {InitialReleaseDate}.",
                latestInitialRelease.CreatedDate);
            return;
        }

        await _salesAutoCreateService.SyncCrmSalesAutoCreateAsync(new SyncCrmSalesAutoCreateCommand
        {
            ServiceProviderId = _salesAutoCreateSetting.ServiceProviderId
        }, cancellationToken).ConfigureAwait(false);
    }
}
