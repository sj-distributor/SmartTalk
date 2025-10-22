using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;

namespace SmartTalk.Core.Services.SpeechMatics;

public partial interface ISpeechMaticsDataProvider : IScopedDependency
{
    Task<SpeechMaticsJob> GetSpeechMaticsJobAsync(string jobId, CancellationToken cancellationToken = default);
    
    Task AddSpeechMaticsJobAsync(SpeechMaticsJob job, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateSpeechMaticsJobAsync(SpeechMaticsJob job, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class SpeechMaticsDataProvider : ISpeechMaticsDataProvider
{
    public async Task<SpeechMaticsJob> GetSpeechMaticsJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<SpeechMaticsJob>().Where(x => x.JobId == jobId)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task AddSpeechMaticsJobAsync(SpeechMaticsJob job, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(job, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSpeechMaticsJobAsync(SpeechMaticsJob job, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}