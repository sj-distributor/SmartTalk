using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestActionHandler : IScopedDependency
{
    AutoTestActionType ActionType { get; }
    
    Task<string> ActionHandleAsync(AutoTestScenario scenario, CancellationToken cancellationToken = default);
}

public class WebhookAutoTestHandler : IAutoTestActionHandler
{
    public AutoTestActionType ActionType => AutoTestActionType.Webhook;
    
    public async Task<string> ActionHandleAsync(AutoTestScenario scenario, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}