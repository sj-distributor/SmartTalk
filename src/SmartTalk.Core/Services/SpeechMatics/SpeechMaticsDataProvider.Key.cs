using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;

namespace SmartTalk.Core.Services.SpeechMatics;

public partial interface ISpeechMaticsDataProvider : IScopedDependency
{
    Task<List<SpeechMaticsKey>> GetSpeechMaticsKeysAsync(List<SpeechMaticsKeyStatus> status = null, DateTimeOffset? lastModifiedDate = null, CancellationToken cancellationToken = default);

    Task UpdateSpeechMaticsKeysAsync(List<SpeechMaticsKey> speechMaticsKeys, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class SpeechMaticsDataProvider : ISpeechMaticsDataProvider
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

    public async Task UpdateSpeechMaticsKeysAsync(List<SpeechMaticsKey> speechMaticsKeys, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(speechMaticsKeys, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}