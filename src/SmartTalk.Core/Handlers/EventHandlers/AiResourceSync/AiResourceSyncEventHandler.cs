using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Events.AiResourceSync;

namespace SmartTalk.Core.Handlers.EventHandlers.AiResourceSync;

public class AiResourceSyncEventHandler : IEventHandler<AiResourceSyncEvent>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public AiResourceSyncEventHandler(ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public Task Handle(IReceiveContext<AiResourceSyncEvent> context, CancellationToken cancellationToken)
    {
        _backgroundJobClient.Enqueue<IAiResourceSyncProcessJobService>(
            x => x.ExecuteSyncCrmSalesAutoCreateAsync(context.Message.Command, context.Message.CrmSalesAuto ?? new List<SmartTalk.Messages.Dto.Sales.CrmSalesAutoSyncCustomerDto>(), CancellationToken.None));

        return Task.CompletedTask;
    }
}
