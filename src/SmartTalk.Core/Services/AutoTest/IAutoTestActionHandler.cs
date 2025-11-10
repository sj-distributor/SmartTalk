using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestActionHandler : IScopedDependency
{
    AutoTestActionType ActionType { get; }
    
    Task<string> ActionHandleAsync(AutoTestScenario scenario, int taskId, CancellationToken cancellationToken = default);
}

public class WebhookAutoTestHandler : IAutoTestActionHandler
{
    // API
    public AutoTestActionType ActionType => AutoTestActionType.Api;
    
    // SalesOrder scenario
    
    public async Task<string> ActionHandleAsync(AutoTestScenario scenario, int taskId, CancellationToken cancellationToken = default)
    {
        // 1. 判断scenario的ActionConfig是否为空
        // 2. 根据ActionConfig去执行（提取url、headers、http_method、body字段）
        return await Task.FromResult("");
    }
}