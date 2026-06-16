using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
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
        var (customers, totalCount) = await _crmClient.GetSalesAutoSyncCustomersAsync(isGetTotalCount: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        _backgroundJobClient.Enqueue<IAiResourceSyncProcessJobService>(
            x => x.ExecuteSyncCrmSalesAutoCreateAsync(context.Message.Command, customers, CancellationToken.None));

    }
}
