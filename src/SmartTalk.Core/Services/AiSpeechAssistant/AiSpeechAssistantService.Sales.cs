using System.Text;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<string> HandleOrderArrivalTimeList(List<string> custmoerIds, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<string> HandleOrderArrivalTimeList(List<string> customerIds, CancellationToken cancellationToken)
    {
        var getOrderArrivalTimeList = await _salesClient.GetOrderArrivalTimeAsync(new GetOrderArrivalTimeRequestDto
        {
            CustomerIds = customerIds
        }, cancellationToken).ConfigureAwait(false);

        if (getOrderArrivalTimeList.Data.Count == 0) return "这位客户暂时没有订单。";
        
        var resultBuilder = new StringBuilder();
        
        var notDeliveredOrders = getOrderArrivalTimeList.Data
            .Where(order => new[] { 0, 1, 2, 3, 5, 6, 8 }.Contains(order.OrderStatus))
            .ToList();
        
        var deliveringOrders = getOrderArrivalTimeList.Data
            .Where(order => order.OrderStatus == 4)
            .ToList();
        
        var completedOrders = getOrderArrivalTimeList.Data
            .Where(order => order.OrderStatus == 7)
            .ToList();
        
        AppendOrderSection(resultBuilder, "未配送", notDeliveredOrders);
        AppendOrderSection(resultBuilder, "配送中", deliveringOrders);
        AppendOrderSection(resultBuilder, "已完成", completedOrders);

        return resultBuilder.ToString();
    }
    
    private void AppendOrderSection(StringBuilder builder, string sectionName, List<GetOrderArrivalTimeDataDto> orders)
    {
        if (orders.Count > 0)
        {
            builder.AppendLine($"{sectionName}：");
            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                builder.AppendLine(
                    $"{i + 1}. 订单号码：{order.SalesOrderNumber} ，客户ID：{order.CustomerId} ，预计送到时间：{order.EstimatedDeliveryTime}");
            }
            builder.AppendLine();
        }
    }
}