using Hangfire.Throttling;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.AiResourceSync;

namespace SmartTalk.Core.Services.AiResourceSync;

public partial interface IAiResourceSyncProcessJobService: IScopedDependency
{
    [Semaphore(HangfireConstants.SemaphoreRefreshCrmCustomerContactPhoneMap)]
    Task RefreshCrmCustomerContactPhoneMapsAsync(SchedulingRefreshCrmCustomerContactPhoneMapCommand command, CancellationToken cancellationToken);
}

public class AiResourceSyncProcessJobService : IAiResourceSyncProcessJobService
{
    private readonly IAiResourceSyncService _aiResourceSyncService;

    public AiResourceSyncProcessJobService(IAiResourceSyncService aiResourceSyncService)
    {
        _aiResourceSyncService = aiResourceSyncService;
    }
    
    public async Task RefreshCrmCustomerContactPhoneMapsAsync(SchedulingRefreshCrmCustomerContactPhoneMapCommand command, CancellationToken cancellationToken)
    {
        await _aiResourceSyncService.RefreshCrmCustomerContactPhoneMapsAsync(cancellationToken).ConfigureAwait(false);
    }
}