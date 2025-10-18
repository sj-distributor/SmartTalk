using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest.AiOrder;

public class AiOrderAutoTestHandler : IAutoTestRunningHandler
{
    public AutoTestRunningType RunningType => AutoTestRunningType.AiOrder;
    
    public async Task<string> ActionHandleAsync(AutoTestScenario scenario, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}