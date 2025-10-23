using System.Text.Json;
using Hangfire;
using Serilog;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Enums.SpeechMatics;

namespace SmartTalk.Core.Services.SpeechMatics;

public interface ISpeechMaticsJobService : IScopedDependency
{
    Task UploadSpeechMaticsKeysAsync(CancellationToken cancellationToken);
    
    Task ScheduleRefreshCustomerItemsCacheAsync(RefreshAllCustomerItemsCacheCommand command, CancellationToken cancellationToken);

    Task RefreshCustomerItemsCacheBySoldToIdAsync(string soldToId, CancellationToken cancellationToken);
}

public class SpeechMaticsJobService : ISpeechMaticsJobService
{
    private readonly AiSpeechAssistantService _aiSpeechAssistantService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ISpeechMaticsDataProvider _speechMaticsDataProvider;

    public SpeechMaticsJobService(AiSpeechAssistantService aiSpeechAssistantService, ISpeechMaticsDataProvider speechMaticsDataProvider)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
        _speechMaticsDataProvider = speechMaticsDataProvider;
    }

    public async Task UploadSpeechMaticsKeysAsync(CancellationToken cancellationToken)
    {
        var currentDateTimeOffset = DateTimeOffset.UtcNow;
        
        var firstDayOfMonth = new DateTimeOffset(currentDateTimeOffset.Year, currentDateTimeOffset.Month, 1, 0, 0, 0, TimeSpan.Zero);
        
        var speechMaticsKeys = await _speechMaticsDataProvider.GetSpeechMaticsKeysAsync([SpeechMaticsKeyStatus.Discard], firstDayOfMonth, cancellationToken).ConfigureAwait(false);
        
        speechMaticsKeys.ForEach(x => { 
            x.Status = SpeechMaticsKeyStatus.NotEnabled;
            x.LastModifiedDate = null;
        });
        
        await _speechMaticsDataProvider.UpdateSpeechMaticsKeysAsync(speechMaticsKeys, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task ScheduleRefreshCustomerItemsCacheAsync(RefreshAllCustomerItemsCacheCommand command, CancellationToken cancellationToken)
    {
        Log.Information("Start full customer items cache refresh...");

        var allSales = await _speechMaticsDataProvider.GetAllSalesAsync(cancellationToken).ConfigureAwait(false);
        var allSoldToIds = allSales.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();

        foreach (var soldToId in allSoldToIds)
        {
            _backgroundJobClient.Enqueue<ISpeechMaticsJobService>(x => x.RefreshCustomerItemsCacheBySoldToIdAsync(soldToId, CancellationToken.None));
        }

        Log.Information("All customer items cache refresh jobs scheduled. Count: {Count}", allSoldToIds.Count);
    }

    public async Task RefreshCustomerItemsCacheBySoldToIdAsync(string soldToId, CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("Refreshing cache for soldToId: {SoldToId}", soldToId);

            var itemsString = await _aiSpeechAssistantService.BuildCustomerItemsStringAsync(new List<string> { soldToId }, cancellationToken);
            
            await _speechMaticsDataProvider.UpsertCustomerItemsCacheAsync(soldToId, itemsString, cancellationToken).ConfigureAwait(false);

            Log.Information("Cache refreshed successfully for soldToId: {SoldToId}", soldToId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh cache for soldToId: {SoldToId}", soldToId);
        }
    }
}