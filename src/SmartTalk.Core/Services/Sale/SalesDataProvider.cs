using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.Sales;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesDataProvider : IScopedDependency
{
    Task<List<Sales>> GetAllSalesAsync(CancellationToken cancellationToken);
    
    Task<Sales> GetCallInSalesByNameAsync(string assistantName, SalesCallType? type, CancellationToken cancellationToken);
    
    Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetCustomerItemsCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, bool forceSave, CancellationToken cancellationToken);

    Task UpsertCustomerInfoCacheAsync(string phoneNumber, string cacheValue, bool forceSave, CancellationToken cancellationToken);

    Task UpsertDeliveryInfoCacheAsync(string phoneNumber, string cacheValue, bool forceSave, CancellationToken cancellationToken);
    
    Task<AiSpeechAssistantKnowledgeVariableCache> GetCustomerInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);

    Task<AiSpeechAssistantKnowledgeVariableCache> GetDeliveryInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
}

public class SalesDataProvider : ISalesDataProvider
{
    private const string CustomerItemsCacheKey = "customer_items";
    private const string CustomerInfoCacheKey = "customer_info";
    private const string DeliveryInfoCacheKey = "delivery_info";

    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SalesDataProvider(IRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<Sales>> GetAllSalesAsync(CancellationToken cancellationToken)
    {
        return await _repository.GetAllAsync<Sales>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Sales> GetCallInSalesByNameAsync(string assistantName, SalesCallType? type, CancellationToken cancellationToken)
    {
        var query = _repository.Query<Sales>().Where(s => s.Name == assistantName);

        if (type.HasValue) query = query.Where(s => s.Type == type.Value);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetCustomerItemsCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantKnowledgeVariableCache>()
            .Where(x => x.CacheKey == CustomerItemsCacheKey && soldToIds.Contains(x.Filter))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, bool forceSave, CancellationToken cancellationToken)
    {
        var cache = await _repository.FirstOrDefaultAsync<AiSpeechAssistantKnowledgeVariableCache>(
            x => x.CacheKey == CustomerItemsCacheKey && x.Filter == soldToId, cancellationToken);
        if (cache == null)
        {
            cache = new AiSpeechAssistantKnowledgeVariableCache
            {
                CacheKey = CustomerItemsCacheKey,
                Filter = soldToId,
                CacheValue = itemsString,
                LastUpdated = DateTimeOffset.UtcNow
            };
            await _repository.InsertAsync(cache, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            cache.CacheValue = itemsString;
            cache.LastUpdated = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(cache, cancellationToken).ConfigureAwait(false);
        }

        if (forceSave)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    
    public async Task UpsertCustomerInfoCacheAsync(string phoneNumber, string cacheValue, bool forceSave, CancellationToken cancellationToken)
    {
        await UpsertPhoneScopedCacheAsync(CustomerInfoCacheKey, phoneNumber, cacheValue, forceSave, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertDeliveryInfoCacheAsync(string phoneNumber, string cacheValue, bool forceSave, CancellationToken cancellationToken)
    {
        await UpsertPhoneScopedCacheAsync(DeliveryInfoCacheKey, phoneNumber, cacheValue, forceSave, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantKnowledgeVariableCache> GetCustomerInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        return await GetPhoneScopedCacheByPhoneNumberAsync(CustomerInfoCacheKey, phoneNumber, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantKnowledgeVariableCache> GetDeliveryInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        return await GetPhoneScopedCacheByPhoneNumberAsync(DeliveryInfoCacheKey, phoneNumber, cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertPhoneScopedCacheAsync(
        string cacheKey,
        string phoneNumber,
        string cacheValue,
        bool forceSave,
        CancellationToken cancellationToken)
    {
        var cache = await _repository.FirstOrDefaultAsync<AiSpeechAssistantKnowledgeVariableCache>(
            x => x.CacheKey == cacheKey && x.Filter == phoneNumber, cancellationToken).ConfigureAwait(false);

        if (cache == null)
        {
            cache = new AiSpeechAssistantKnowledgeVariableCache
            {
                CacheKey = cacheKey,
                Filter = phoneNumber,
                CacheValue = cacheValue,
                LastUpdated = DateTimeOffset.UtcNow
            };
            await _repository.InsertAsync(cache, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            cache.CacheValue = cacheValue;
            cache.LastUpdated = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(cache, cancellationToken).ConfigureAwait(false);
        }

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<AiSpeechAssistantKnowledgeVariableCache> GetPhoneScopedCacheByPhoneNumberAsync(
        string cacheKey,
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        var candidates = BuildPhoneCandidates(phoneNumber);
        if (candidates.Count == 0) return null;

        var caches = await _repository.Query<AiSpeechAssistantKnowledgeVariableCache>()
            .Where(x => x.CacheKey == cacheKey && candidates.Contains(x.Filter))
            .OrderByDescending(x => x.LastUpdated)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var candidate in candidates)
        {
            var match = caches.FirstOrDefault(x => x.Filter == candidate);
            if (match != null) return match;
        }

        return null;
    }

    private static List<string> BuildPhoneCandidates(string phoneNumber)
    {
        var candidates = new List<string>();

        void AddCandidate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !candidates.Contains(value))
                candidates.Add(value);
        }

        AddCandidate(phoneNumber?.Trim());

        var digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        AddCandidate(digits);

        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal))
            AddCandidate("+" + digits);

        if (digits.Length == 10)
        {
            AddCandidate("+1" + digits);
            AddCandidate("1" + digits);
        }

        return candidates;
    }
}
