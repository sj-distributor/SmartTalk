using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider
{
    Task<AutoTestScenario> GetAutoTestScenarioByIdAsync(int id, CancellationToken cancellationToken);
}

public partial class AutoTestDataProvider
{
    public async Task<AutoTestScenario> GetAutoTestScenarioByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.QueryNoTracking<AutoTestScenario>().Where(x => x.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}