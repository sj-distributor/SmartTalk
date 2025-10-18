using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestRunningHandler : IScopedDependency
{
    AutoTestRunningType RunningType { get; }
    
    Task<string> ActionHandleAsync(AutoTestScenario scenario, CancellationToken cancellationToken = default);
}