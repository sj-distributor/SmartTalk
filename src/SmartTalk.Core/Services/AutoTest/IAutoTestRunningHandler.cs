using SmartTalk.Core.Domain.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestRunningHandler
{
    Task<string> ActionHandleAsync(AutoTestScenario scenario, CancellationToken cancellationToken = default);
}