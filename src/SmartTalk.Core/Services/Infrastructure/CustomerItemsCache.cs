using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SmartTalk.Core.Services.SpeechMatics;

namespace SmartTalk.Core.Services.Infrastructure;

public class CustomerItemsCache
{
    private readonly ISpeechMaticsService _speechMaticsService;
    private readonly ILogger<CustomerItemsCache> _logger;
    
    private readonly ConcurrentDictionary<string, CachedItemsEntry> _cache = new();
    
    private class CachedItemsEntry
    {
        public List<string> Items { get; set; } = new();
        public DateTime LastRefresh { get; set; } = DateTime.MinValue;
        public SemaphoreSlim Semaphore { get; set; } = new(1, 1);
    }

    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(30);

    public CustomerItemsCache(ISpeechMaticsService speechMaticsService, ILogger<CustomerItemsCache> logger)
    {
        _speechMaticsService = speechMaticsService;
        _logger = logger;
    }

    public async Task<List<string>> GetCustomerItemsAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        var key = string.Join(',', soldToIds.OrderBy(x => x));

        var entry = _cache.GetOrAdd(key, _ => new CachedItemsEntry());

        if (DateTime.UtcNow - entry.LastRefresh > _refreshInterval)
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            try
            {
                if (DateTime.UtcNow - entry.LastRefresh > _refreshInterval)
                {
                    _logger.LogInformation("Refreshing customer items cache for soldToIds: {SoldToIds}", key);

                    var itemsString = await _speechMaticsService.BuildCustomerItemsStringAsync(soldToIds, cancellationToken)
                        .ConfigureAwait(false);

                    entry.Items = new List<string>(itemsString?.Split(Environment.NewLine) ?? Array.Empty<string>());
                    entry.LastRefresh = DateTime.UtcNow;
                }
            }
            finally
            {
                entry.Semaphore.Release();
            }
        }

        return entry.Items;
    }
}