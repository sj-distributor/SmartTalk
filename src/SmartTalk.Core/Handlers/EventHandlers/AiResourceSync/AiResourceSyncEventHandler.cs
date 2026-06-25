using Mediator.Net.Context;
using Mediator.Net.Contracts;
using Serilog;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Commands.AiResourceSync;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Events.AiResourceSync;

namespace SmartTalk.Core.Handlers.EventHandlers.AiResourceSync;

public class AiResourceSyncEventHandler : IEventHandler<AiResourceSyncEvent>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public AiResourceSyncEventHandler(ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task Handle(IReceiveContext<AiResourceSyncEvent> context, CancellationToken cancellationToken)
    {
        Log.Information("Start AiResourceSyncEventHandler");

        _backgroundJobClient.Enqueue<IAiResourceSyncProcessJobService>(
            x => x.ExecuteSyncCrmSalesAutoCreateAsync(
                new AiResourceSyncCommand
                {
                    ServiceProviderId = context.Message.ServiceProviderId,
                    IsManual = context.Message.IsManual,
                    InitiatedByUserId = context.Message.InitiatedByUserId
                }, new List<CrmSalesAutoSyncCustomerDto>(), CancellationToken.None));
    }
}
