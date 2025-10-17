using SmartTalk.Core.Domain.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestRunningHandler
{
    Task<string> InputHandleAsync(AutoTestScenario scenario, CancellationToken cancellationToken = default);
    
    Task<string> ActionHandleAsync(AutoTestScenario scenario, string inputJson, CancellationToken cancellationToken = default);
}