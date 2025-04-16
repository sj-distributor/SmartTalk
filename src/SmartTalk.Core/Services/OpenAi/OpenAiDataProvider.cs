using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.OpenAi;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.OpenAi;

public interface IOpenAiDataProvider : IScopedDependency
{
    Task AddOpenAiApiKeyUsageStatusAsync(OpenAiApiKeyUsageStatus status, CancellationToken cancellationToken);

    Task<List<OpenAiApiKeyUsageStatus>> GetOpenAiApiKeyUsageStatusAsync(int? id = null, int? count = null, CancellationToken cancellationToken = default);

    Task UpdateOpenAiApiKeyUsageStatusAsync(OpenAiApiKeyUsageStatus status, CancellationToken cancellationToken);
}

public class OpenAiDataProvider : IOpenAiDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public OpenAiDataProvider(IUnitOfWork unitOfWork, IRepository repository)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
    }

    public async Task AddOpenAiApiKeyUsageStatusAsync(OpenAiApiKeyUsageStatus status, CancellationToken cancellationToken)
    {
        await _repository.InsertAsync(status, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<OpenAiApiKeyUsageStatus>> GetOpenAiApiKeyUsageStatusAsync(int? id = null, int? count = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<OpenAiApiKeyUsageStatus>();

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);
        
        if (count.HasValue)
            query = query.Take(count.Value);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateOpenAiApiKeyUsageStatusAsync(OpenAiApiKeyUsageStatus status, CancellationToken cancellationToken)
    {
        await _repository.UpdateAsync(status, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}