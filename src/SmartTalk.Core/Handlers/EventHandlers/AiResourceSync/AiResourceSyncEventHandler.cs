using Mediator.Net.Context;
using Mediator.Net.Contracts;
using Serilog;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Events.AiResourceSync;

namespace SmartTalk.Core.Handlers.EventHandlers.AiResourceSync;

public class AiResourceSyncEventHandler : IEventHandler<AiResourceSyncEvent>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly ICrmClient _crmClient;

    public AiResourceSyncEventHandler(ISmartTalkBackgroundJobClient backgroundJobClient, ICrmClient crmClient)
    {
        _backgroundJobClient = backgroundJobClient;
        _crmClient = crmClient;
    }

    public async Task Handle(IReceiveContext<AiResourceSyncEvent> context, CancellationToken cancellationToken)
    {
        Log.Information("Start AiResourceSyncEventHandler");

        _backgroundJobClient.Enqueue<IAiResourceSyncProcessJobService>(
            x => x.ExecuteSyncCrmSalesAutoCreateAsync(context.Message.Command, new List<CrmSalesAutoSyncCustomerDto>(), CancellationToken.None));
    }
}
