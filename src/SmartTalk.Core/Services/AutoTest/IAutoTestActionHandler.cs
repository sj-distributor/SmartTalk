using Newtonsoft.Json;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.AutoTest;
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

    private string OrderCompare(List<AutoTestInputDetail> realOrderItems, List<AutoTestInputDetail> aiOrderItems)
    {
        aiOrderItems ??= []; 
        realOrderItems ??= [];
        
        var orderItems = new List<AutoTestOrderItemDto>();
        
        foreach (var realOrderItem in realOrderItems)
        {
            var item = aiOrderItems.FirstOrDefault(x => x.Material == realOrderItem.Material);

            if (item == null)
            {
                orderItems.Add(new AutoTestOrderItemDto
                {
                    Material = realOrderItem.Material,
                    Quantity = realOrderItem.Quantity,
                    MaterialName = realOrderItem.ItemDesc,
                    ItemStatus = AutoTestOrderItemStatus.Missed
                });
                
                continue;
            }
            
            orderItems.Add(new AutoTestOrderItemDto
            {
                Material = item.Material,
                Quantity = item.Quantity,
                MaterialName = item.ItemDesc,
                ItemStatus = realOrderItem.Quantity == item.Quantity ? AutoTestOrderItemStatus.Normal : AutoTestOrderItemStatus.Abnormal
            });
        }

        foreach (var aiItem in aiOrderItems)
        {
            if (!realOrderItems.Any(x => x.Material == aiItem.Material))
            {
                orderItems.Add(new AutoTestOrderItemDto
                {
                    Material = aiItem.Material,
                    Quantity = aiItem.Quantity,
                    MaterialName = aiItem.ItemDesc,
                    ItemStatus = AutoTestOrderItemStatus.Abnormal
                });
            }
        }
        
        return JsonConvert.SerializeObject(orderItems);
    }
}