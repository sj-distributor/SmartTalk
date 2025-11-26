using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Handlers.CommandHandlers.Sales;

public class RefreshAllCustomerInfoCacheCommandHandler : ICommandHandler<RefreshAllCustomerInfoCacheCommand>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public RefreshAllCustomerInfoCacheCommandHandler(ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task Handle(IReceiveContext<RefreshAllCustomerInfoCacheCommand> context, CancellationToken cancellationToken)
    {
        _backgroundJobClient.Enqueue<ISalesJobProcessJobService>(
            x => x.ScheduleRefreshCrmCustomerInfoAsync(context.Message, cancellationToken), HangfireConstants.InternalHostingCaCheKnowledgeVariable);
    }
}