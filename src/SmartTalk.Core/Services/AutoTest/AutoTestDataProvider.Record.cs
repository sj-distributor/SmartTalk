using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider
{
    Task<AutoTestTaskRecord> GetAutoTestTaskRecordBySpeechMaticsJobIdAsync(string speechMaticsJobId, CancellationToken cancellationToken = default);
}

public partial class AutoTestDataProvider
{
    public async Task<AutoTestTaskRecord> GetAutoTestTaskRecordBySpeechMaticsJobIdAsync(string speechMaticsJobId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AutoTestTaskRecord>()
            .Where(x => x.SpeechMaticsJobId == speechMaticsJobId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}