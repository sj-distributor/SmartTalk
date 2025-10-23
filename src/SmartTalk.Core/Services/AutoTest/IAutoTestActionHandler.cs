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
    public AutoTestActionType ActionType => AutoTestActionType.Webhook;
    
    public async Task<string> ActionHandleAsync(AutoTestScenario scenario, int taskId, CancellationToken cancellationToken = default)
    {
        // TODO：要实时获取 taskId 相关数据集，即要实时 TestTaskRecord 状态为 Ongoing 的 dataItem 去执行
        // TODO: 执行完成需要 update 每条 TestTaskRecord 的状态为done
        throw new NotImplementedException();
    }
}