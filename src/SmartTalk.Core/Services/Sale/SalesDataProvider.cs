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

    Task UpsertCustomerInfoCacheAsync(string phoneNumber, string itemsString, bool forceSave, CancellationToken cancellationToken);
    
    Task<AiSpeechAssistantKnowledgeVariableCache> GetCustomerInfoCacheByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
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
}