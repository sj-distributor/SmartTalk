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
    
    Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetCustomerItemsCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, bool forceSave, CancellationToken cancellationToken);

    Task UpsertCustomerInfoCacheAsync(string phoneNumber, string itemsString, bool forceSave, CancellationToken cancellationToken);
    
    Task<AiSpeechAssistantKnowledgeVariableCache> GetCustomerInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);

    Task AddPhoneOrderPushTaskAsync(PhoneOrderPushTask task, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<PhoneOrderPushTask> GetNextExecutableTaskAsync(int assistantId, CancellationToken cancellationToken);
    
    Task MarkSendingAsync(int taskId, bool forceSave, CancellationToken cancellationToken = default);

    Task MarkSentAsync(int taskId, bool forceSave, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(int taskId, bool forceSave, CancellationToken cancellationToken = default);

    Task<bool> HasPendingTasksByRecordIdAsync(int recordId, CancellationToken cancellationToken);
}

public class SalesDataProvider : ISalesDataProvider
{
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
        return await _repository.Query<AiSpeechAssistantKnowledgeVariableCache>().Where(x => soldToIds.Contains(x.Filter)).ToListAsync(cancellationToken);
    }

    public async Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, bool forceSave, CancellationToken cancellationToken)
    {
        var cache = await _repository.FirstOrDefaultAsync<AiSpeechAssistantKnowledgeVariableCache>(x => x.Filter == soldToId, cancellationToken);
        if (cache == null)
        {
            cache = new AiSpeechAssistantKnowledgeVariableCache
            {
                CacheKey = "customer_items",
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
    
    public async Task UpsertCustomerInfoCacheAsync(string phoneNumber, string itemsString, bool forceSave, CancellationToken cancellationToken)
    {
        var cache = await _repository.FirstOrDefaultAsync<AiSpeechAssistantKnowledgeVariableCache>(x => x.Filter == phoneNumber, cancellationToken);
        if (cache == null)
        {
            cache = new AiSpeechAssistantKnowledgeVariableCache
            {
                CacheKey = "customer_info",
                Filter = phoneNumber,
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

    public async Task<AiSpeechAssistantKnowledgeVariableCache> GetCustomerInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantKnowledgeVariableCache>().Where(x => x.Filter == phoneNumber).FirstOrDefaultAsync(cancellationToken);
    }
    
    public async Task AddPhoneOrderPushTaskAsync(PhoneOrderPushTask task, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(task, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PhoneOrderPushTask> GetNextExecutableTaskAsync(int assistantId, CancellationToken cancellationToken)
    {
        var query = from task in _repository.Query<PhoneOrderPushTask>()
            join record in _repository.Query<PhoneOrderRecord>() on task.ParentRecordId equals record.Id into recordJoin
            from record in recordJoin.DefaultIfEmpty()
            where task.AssistantId == assistantId && task.Status == PhoneOrderPushTaskStatus.Pending && (task.ParentRecordId == null || record.IsCompleted)
            orderby task.CreatedAt
            select task;

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<bool> HasPendingTasksByRecordIdAsync(int recordId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderPushTask>().AnyAsync(t => t.RecordId == recordId && t.Status != PhoneOrderPushTaskStatus.Sent, cancellationToken).ConfigureAwait(false);
    }
}