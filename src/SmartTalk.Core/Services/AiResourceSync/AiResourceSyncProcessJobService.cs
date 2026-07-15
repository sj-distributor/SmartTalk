using Hangfire.Throttling;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.AiResourceSync;
using SmartTalk.Messages.Constants;

namespace SmartTalk.Core.Services.AiResourceSync;

public partial interface IAiResourceSyncProcessJobService: IScopedDependency
{
    [Semaphore(HangfireConstants.SemaphoreSyncCrmSalesAutoCreate)]
    
    Task AiResourceSyncAsync (SchedulingAiResourceSyncCommand command, CancellationToken cancellationToken);
    
    Task ExecuteSyncCrmSalesAutoCreateAsync(AiResourceSyncCommand command, CancellationToken cancellationToken);
}

public class AiResourceSyncProcessJobService : IAiResourceSyncProcessJobService
{
    private readonly IAiResourceSyncService _aiResourceSyncService;

    public AiResourceSyncProcessJobService(IAiResourceSyncService aiResourceSyncService)
    {
        _aiResourceSyncService = aiResourceSyncService;
    }

    private const int MaxSyncAttempts = 3;
    private const int RetryDelaySeconds = 300;

    public async Task AiResourceSyncAsync(SchedulingAiResourceSyncCommand command, CancellationToken cancellationToken)
    { 
       await ExecuteSyncCrmSalesAutoCreateAsync(new AiResourceSyncCommand
       {
           ServiceProviderId = 1,
           InitiatedByUserId = CurrentUsers.InternalUser.Id
       }, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExecuteSyncCrmSalesAutoCreateAsync(AiResourceSyncCommand command, CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (var attempt = 1; attempt <= MaxSyncAttempts; attempt++)
        {
            try
            {
                var executionResult = await _aiResourceSyncService.SyncInternalAsync(command, cancellationToken).ConfigureAwait(false);

                await _aiResourceSyncService.RecordSyncRunAsync(command, executionResult.Stats, executionResult.IsInitialRelease, true, null, cancellationToken).ConfigureAwait(false);

                if (!command.IsManual)
                    await _aiResourceSyncService.SendNotifyAsync(true, cancellationToken).ConfigureAwait(false);

                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Log.Warning(ex, "SyncCrmSalesAutoCreate attempt {Attempt}/{MaxAttempts} failed", attempt, MaxSyncAttempts);

                if (attempt < MaxSyncAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(RetryDelaySeconds), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                Log.Error(ex, "SyncCrmSalesAutoCreate failed after {MaxAttempts} attempts", MaxSyncAttempts);
                await _aiResourceSyncService.RecordSyncRunAsync(command, null, false, false, ex.Message, cancellationToken).ConfigureAwait(false);

                if (!command.IsManual)
                    await _aiResourceSyncService.SendNotifyAsync(false, cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastException!;
    }
}
