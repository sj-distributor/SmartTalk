using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.Sales;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesDataProvider : IScopedDependency
{
    Task<List<Sales>> GetAllSalesAsync(CancellationToken cancellationToken);

    Task<Sales> GetCallInSalesByNameAsync(string assistantName, SalesCallType? type, CancellationToken cancellationToken);

    Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetCustomerItemsCacheByAssistantNameAsync(string assistantName, CancellationToken cancellationToken);
    Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetCustomerItemsCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetDeliveryProgressCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, bool forceSave, CancellationToken cancellationToken);

    Task UpsertDeliveryProgressCacheAsync(string soldToId, string deliveryProgressString, bool forceSave, CancellationToken cancellationToken);

    Task UpsertCustomerInfoCacheAsync(string phoneNumber, string cacheValue, bool forceSave, CancellationToken cancellationToken);

    Task UpsertDeliveryInfoCacheAsync(string phoneNumber, string cacheValue, bool forceSave, CancellationToken cancellationToken);

    Task<AiSpeechAssistantKnowledgeVariableCache> GetCustomerInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);

    Task<AiSpeechAssistantKnowledgeVariableCache> GetDeliveryInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);

    Task AddPhoneOrderPushTaskAsync(PhoneOrderPushTask task, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task MarkSendingAsync(int taskId, bool forceSave, CancellationToken cancellationToken = default);

    Task MarkSentAsync(int taskId, bool forceSave, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(int taskId, bool forceSave, CancellationToken cancellationToken = default);

    Task<bool> IsParentCompletedAsync(int? parentRecordId, CancellationToken cancellationToken);

    Task<bool> HasPendingTasksByRecordIdAsync(int recordId, CancellationToken cancellationToken);
    
    Task<PhoneOrderPushTask> GetRecordPushTaskByRecordIdAsync(int recordId, CancellationToken cancellationToken);
}

public class SalesDataProvider : ISalesDataProvider
{
    private const string CustomerItemsCacheKey = "customer_items";
    private const string DeliveryProgressCacheKey = "delivery_progress";
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

    public async Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetCustomerItemsCacheByAssistantNameAsync(string assistantName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assistantName))
            return [];

        var filters = assistantName
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var trimmedAssistantName = assistantName.Trim();
        if (!filters.Contains(trimmedAssistantName, StringComparer.OrdinalIgnoreCase))
            filters.Add(trimmedAssistantName);

        return await GetKnowledgeVariableCachesByFiltersAsync(CustomerItemsCacheKey, filters, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetCustomerItemsCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        return await GetKnowledgeVariableCachesByFiltersAsync(CustomerItemsCacheKey, soldToIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetDeliveryProgressCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        return await GetKnowledgeVariableCachesByFiltersAsync(DeliveryProgressCacheKey, soldToIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, bool forceSave, CancellationToken cancellationToken)
    {
        await UpsertKnowledgeVariableCacheAsync(CustomerItemsCacheKey, soldToId, itemsString, forceSave, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertDeliveryProgressCacheAsync(string soldToId, string deliveryProgressString, bool forceSave, CancellationToken cancellationToken)
    {
        await UpsertKnowledgeVariableCacheAsync(DeliveryProgressCacheKey, soldToId, deliveryProgressString, forceSave, cancellationToken).ConfigureAwait(false);
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

    public async Task AddPhoneOrderPushTaskAsync(PhoneOrderPushTask task, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(task, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task MarkSendingAsync(int taskId, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var task = await _repository.Query<PhoneOrderPushTask>().Where(t => t.Id == taskId).FirstOrDefaultAsync(cancellationToken);

        if (task == null) return;

        task.Status = PhoneOrderPushTaskStatus.Sending;

        await _repository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkSentAsync(int taskId, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var task = await _repository.Query<PhoneOrderPushTask>().Where(t => t.Id == taskId).FirstOrDefaultAsync(cancellationToken);

        if (task == null) return;
        
        task.Status = PhoneOrderPushTaskStatus.Sent;

        await _repository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(int taskId, bool forceSave, CancellationToken cancellationToken = default)
    {
        var task = await _repository.Query<PhoneOrderPushTask>().Where(t => t.Id == taskId).FirstOrDefaultAsync(cancellationToken);
        
        if (task == null) return;
        
        task.Status = PhoneOrderPushTaskStatus.Failed;

        await _repository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<bool> IsParentCompletedAsync(int? parentRecordId, CancellationToken cancellationToken)
    {
        if (!parentRecordId.HasValue) return true;

        return await _repository.Query<PhoneOrderRecord>().Where(r => r.Id == parentRecordId.Value).Select(r => r.IsCompleted).FirstOrDefaultAsync(cancellationToken);
    }


    public async Task<bool> HasPendingTasksByRecordIdAsync(int recordId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderPushTask>().AnyAsync(t => t.RecordId == recordId && t.Status != PhoneOrderPushTaskStatus.Sent, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PhoneOrderPushTask> GetRecordPushTaskByRecordIdAsync(int recordId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderPushTask>().Where(t => t.RecordId == recordId && t.Status == PhoneOrderPushTaskStatus.Pending)
            .OrderBy(t => t.CreatedAt).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}
