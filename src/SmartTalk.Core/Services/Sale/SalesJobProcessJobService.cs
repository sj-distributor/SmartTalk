using Serilog;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Services.Sale;

public class SalesJobProcessJobService
{
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
            var ids = soldToId.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            Log.Information("Refreshing cache for soldToId: {SoldToId}", ids);

            var allItems = new List<string>();
            foreach (var id in ids)
            {
                var items = await _aiSpeechAssistantService.BuildCustomerItemsStringAsync(new List<string> { id }, cancellationToken).ConfigureAwait(false);
                allItems.Add(items);
            }

            var combinedItems = string.Join(";", allItems);
            await _aiSpeechAssistantDataProvider.UpsertCustomerItemsCacheAsync(soldToId, combinedItems, true, cancellationToken).ConfigureAwait(false);

            Log.Information("Cache refreshed successfully for soldToId: {SoldToId}", soldToId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh cache for soldToId: {SoldToId}", soldToId);
        }
    }
}