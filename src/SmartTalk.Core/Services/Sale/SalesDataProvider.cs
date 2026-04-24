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

    Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetCustomerOrderArrivalTimeCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, bool forceSave, CancellationToken cancellationToken);

    Task UpsertCustomerOrderArrivalTimeCacheAsync(string soldToId, string orderArrivalTimeString, bool forceSave, CancellationToken cancellationToken);

    Task UpsertCustomerInfoCacheAsync(string phoneNumber, string cacheValue, bool forceSave, CancellationToken cancellationToken);

    Task UpsertDeliveryInfoCacheAsync(string phoneNumber, string cacheValue, bool forceSave, CancellationToken cancellationToken);

    Task<AiSpeechAssistantKnowledgeVariableCache> GetCustomerInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);

    Task<AiSpeechAssistantKnowledgeVariableCache> GetDeliveryInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
}

public class SalesDataProvider : ISalesDataProvider
{
    private const string CustomerItemsCacheKey = "customer_items";
    private const string CustomerOrderArrivalTimeCacheKey = "customer_order_arrival_time";
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

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetCustomerItemsCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        return await GetKnowledgeVariableCachesByFiltersAsync(CustomerItemsCacheKey, soldToIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetCustomerOrderArrivalTimeCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        return await GetKnowledgeVariableCachesByFiltersAsync(CustomerOrderArrivalTimeCacheKey, soldToIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, bool forceSave, CancellationToken cancellationToken)
    {
        await UpsertKnowledgeVariableCacheAsync(CustomerItemsCacheKey, soldToId, itemsString, forceSave, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertCustomerOrderArrivalTimeCacheAsync(string soldToId, string orderArrivalTimeString, bool forceSave, CancellationToken cancellationToken)
    {
        await UpsertKnowledgeVariableCacheAsync(CustomerOrderArrivalTimeCacheKey, soldToId, orderArrivalTimeString, forceSave, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertCustomerInfoCacheAsync(string phoneNumber, string cacheValue, bool forceSave, CancellationToken cancellationToken)
    {
        await UpsertKnowledgeVariableCacheAsync(CustomerInfoCacheKey, phoneNumber, cacheValue, forceSave, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertDeliveryInfoCacheAsync(string phoneNumber, string cacheValue, bool forceSave, CancellationToken cancellationToken)
    {
        await UpsertKnowledgeVariableCacheAsync(DeliveryInfoCacheKey, phoneNumber, cacheValue, forceSave, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantKnowledgeVariableCache> GetCustomerInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        return await GetPhoneScopedCacheByPhoneNumberAsync(CustomerInfoCacheKey, phoneNumber, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantKnowledgeVariableCache> GetDeliveryInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        return await GetPhoneScopedCacheByPhoneNumberAsync(DeliveryInfoCacheKey, phoneNumber, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetKnowledgeVariableCachesByFiltersAsync(
        string cacheKey,
        List<string> filters,
        CancellationToken cancellationToken)
    {
        if (filters == null || filters.Count == 0) return [];

        return await _repository.Query<AiSpeechAssistantKnowledgeVariableCache>()
            .Where(x => x.CacheKey == cacheKey && filters.Contains(x.Filter))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task UpsertKnowledgeVariableCacheAsync(
        string cacheKey,
        string filter,
        string cacheValue,
        bool forceSave,
        CancellationToken cancellationToken)
    {
        var cache = await _repository.FirstOrDefaultAsync<AiSpeechAssistantKnowledgeVariableCache>(
            x => x.CacheKey == cacheKey && x.Filter == filter,
            cancellationToken).ConfigureAwait(false);

        if (cache == null)
        {
            cache = new AiSpeechAssistantKnowledgeVariableCache
            {
                CacheKey = cacheKey,
                Filter = filter,
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
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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

        AddCandidate(NormalizePhoneFilter(phoneNumber));
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

    private static string NormalizePhoneFilter(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return phoneNumber;

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) return "+1" + digits;
        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal)) return "+" + digits;

        return phoneNumber.Trim();
    }
}
