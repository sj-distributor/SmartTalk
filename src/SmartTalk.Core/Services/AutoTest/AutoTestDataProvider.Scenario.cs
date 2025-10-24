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
        return await _repository.GetByIdAsync<AutoTestScenario>(id, cancellationToken).ConfigureAwait(false);
    }
}