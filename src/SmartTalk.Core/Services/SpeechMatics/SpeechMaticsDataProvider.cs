using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;

namespace SmartTalk.Core.Services.SpeechMatics;

public interface ISpeechMaticsDataProvider : IScopedDependency
{
    Task<List<SpeechMaticsKey>> GetSpeechMaticsKeysAsync(List<SpeechMaticsKeyStatus> status = null, DateTimeOffset? lastModifiedDate = null, CancellationToken cancellationToken = default);

    Task UpdateSpeechMaticsKeysAsync(List<SpeechMaticsKey> speechMaticsKeys, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<Sales>> GetAllSalesAsync(CancellationToken cancellationToken);
    
    Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, CancellationToken cancellationToken);
}

public class SpeechMaticsDataProvider : ISpeechMaticsDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SpeechMaticsDataProvider(IRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<SpeechMaticsKey>> GetSpeechMaticsKeysAsync(List<SpeechMaticsKeyStatus> status = null, DateTimeOffset? lastModifiedDate = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<SpeechMaticsKey>();

        if (status is not { Count: 0 })
            query = query.Where(x => status.Contains(x.Status));

        if (lastModifiedDate.HasValue)
            query = query.Where(x => x.LastModifiedDate < lastModifiedDate);
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSpeechMaticsKeysAsync(
        List<SpeechMaticsKey> speechMaticsKeys, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(speechMaticsKeys, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Sales>> GetAllSalesAsync(CancellationToken cancellationToken)
    {
        return await _repository.GetAllAsync<Sales>(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(itemsString.Split(Environment.NewLine));
    
        var cache = await _repository.FirstOrDefaultAsync<CustomerItemsCache>(x => x.CacheKey == soldToId, cancellationToken);
        if (cache == null)
        {
            cache = new CustomerItemsCache
            {
                CacheKey = soldToId,
                CacheValue = serialized,
                LastUpdated = DateTimeOffset.UtcNow
            };
            await _repository.InsertAsync(cache, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            cache.CacheValue = serialized;
            cache.LastUpdated = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(cache, cancellationToken).ConfigureAwait(false);
        }
    }

}