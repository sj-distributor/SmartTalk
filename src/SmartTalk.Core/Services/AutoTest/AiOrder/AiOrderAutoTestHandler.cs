using SmartTalk.Core.Domain.AutoTest;

namespace SmartTalk.Core.Services.AutoTest.AiOrder;

public class AiOrderAutoTestHandler : IAutoTestRunningHandler
{
    public async Task<string> InputHandleAsync(AutoTestScenario scenario, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<string> ActionHandleAsync(AutoTestScenario scenario, string inputJson, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}