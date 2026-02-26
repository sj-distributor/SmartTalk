using System.Text;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.Crm;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesService : IScopedDependency
{
    Task<string> BuildCustomerItemsStringAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task<string> HandleOrderArrivalTimeList(List<string> customerIds, CancellationToken cancellationToken);

    Task<string> BuildCrmCustomerInfoByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);
}

public class SalesService : ISalesService
{
    private readonly ICrmClient _crmClient;
    private readonly ISalesClient _salesClient;

    public SalesService(ICrmClient crmClient,ISalesClient salesClient)
    {
        _crmClient = crmClient;
        _salesClient = salesClient;
    }
    
    public async Task<string> BuildCustomerItemsStringAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        var allItems = new List<string>();

        if (soldToIds == null || soldToIds.Count == 0)
        {
            Log.Warning("BuildCustomerItemsStringAsync called with empty soldToIds");
            return string.Empty;
        }

        foreach (var soldToId in soldToIds)
        {
            var askInfoResponse = await _salesClient
                .GetAskInfoDetailListByCustomerAsync(
                    new GetAskInfoDetailListByCustomerRequestDto { CustomerNumbers = new List<string> { soldToId } },
                    cancellationToken).ConfigureAwait(false);

            var askItems = askInfoResponse?.Data ?? new List<VwAskDetail>();

            var orderResponse = await _salesClient
                .GetOrderHistoryByCustomerAsync(new GetOrderHistoryByCustomerRequestDto { CustomerNumber = soldToId },
                    cancellationToken).ConfigureAwait(false);

            var orderItems = orderResponse?.Data ?? new List<SalesOrderHistoryDto>();

            var levelCodes = askItems.Where(x => !string.IsNullOrEmpty(x.LevelCode)).Select(x => x.LevelCode)
                .Concat(orderItems.Where(x => !string.IsNullOrEmpty(x.LevelCode)).Select(x => x.LevelCode)).Distinct()
                .ToList();

            var materials = askItems.Where(x => !string.IsNullOrEmpty(x.Material)).Select(x => x.Material)
                .Concat(orderItems.Where(x => !string.IsNullOrEmpty(x.MaterialNumber)).Select(x => x.MaterialNumber))
                .Distinct().ToList();

            var requestDto = new GetCustomerLevel5HabitRequstDto
            {
                CustomerId = soldToId,
                LevelCode5List = levelCodes,
                Material = materials
            };

            Log.Information("Sending GetCustomerLevel5HabitAsync with: {@RequestDto}", requestDto);

            var habitResponse = levelCodes.Any()
                ? await _salesClient.GetCustomerLevel5HabitAsync(requestDto, cancellationToken).ConfigureAwait(false)
                : null;

            Log.Information("GetCustomerLevel5HabitAsync Response: {@HabitResponse}", habitResponse);

            var habitLookup = habitResponse?.HistoryCustomerLevel5HabitDtos?.ToDictionary(h => h.LevelCode5, h => h)
                              ?? new Dictionary<string, HistoryCustomerLevel5HabitDto>();

            string FormatItem(string materialDesc, string levelCode = null, string materialNumber = null)
            {
                var parts = materialDesc?.Split('·') ?? Array.Empty<string>();
                var name = parts.Length > 4 ? $"{parts[0]}{parts[4]}" : parts.FirstOrDefault() ?? "";
                var brand = parts.Length > 1 ? parts[1] : "";
                var size = parts.Length > 3 ? parts[3] : "";

                string aliasText = "";
                MaterialPartInfoDto partInfo = null;

                if (!string.IsNullOrEmpty(levelCode) && habitLookup.TryGetValue(levelCode, out var habit))
                {
                    aliasText = habit.CustomerLikeNames != null && habit.CustomerLikeNames.Any()
                        ? string.Join(", ", habit.CustomerLikeNames.Select(n => n.CustomerLikeName))
                        : "";

                    partInfo = habit.MaterialPartInfoDtos?.FirstOrDefault(p =>
                        string.Equals(p.MaterialNumber, materialNumber, StringComparison.OrdinalIgnoreCase));
                }

                return $"Item: {name}, Brand: {brand}, Size: {size}, Aliases: {aliasText}, " +
                       $"baseUnit: {partInfo?.BaseUnit ?? ""}, salesUnit: {partInfo?.SalesUnit ?? ""}, weights: {partInfo?.Weights ?? 0}, " +
                       $"placeOfOrigin: {partInfo?.PlaceOfOrigin ?? ""}, packing: {partInfo?.Packing ?? ""}, specifications: {partInfo?.Specifications ?? ""}, " +
                       $"ranks: {partInfo?.Ranks ?? ""}, atr: {partInfo?.Atr}";
            }

            allItems.AddRange(askItems.Select(x => FormatItem(x.MaterialDesc, x.LevelCode, x.Material)));
            allItems.AddRange(orderItems.Select(x => FormatItem(x.MaterialDescription, x.LevelCode, x.MaterialNumber)));
            
            var customerOrderArrivalText = await HandleOrderArrivalTimeList(new List<string> { soldToId }, cancellationToken);
            if (!string.IsNullOrEmpty(customerOrderArrivalText))
            {
                allItems.Add($"=== 客户 {soldToId} 订单到货信息 ===");
                allItems.Add(customerOrderArrivalText);
            }
        }

        return string.Join(Environment.NewLine, allItems.Distinct().Take(150));
    }
    
    public async Task<string> HandleOrderArrivalTimeList(List<string> customerIds, CancellationToken cancellationToken)
    {
        var processedCustomerIds = customerIds.Select(id => "0000" + id).ToList();

        var getOrderArrivalTimeList = await _salesClient.GetOrderArrivalTimeAsync(new GetOrderArrivalTimeRequestDto { CustomerIds = processedCustomerIds }, cancellationToken).ConfigureAwait(false);

        if (getOrderArrivalTimeList.Data.Count == 0) return "这位客户暂时没有订单。";
        
        var resultBuilder = new StringBuilder();
        
        var notDeliveredOrders = getOrderArrivalTimeList.Data.Where(order => new[] { 0, 1, 2, 3, 5, 6, 8 }.Contains(order.OrderStatus)).ToList();
        
        var deliveringOrders = getOrderArrivalTimeList.Data.Where(order => order.OrderStatus == 4).ToList();
        
        var completedOrders = getOrderArrivalTimeList.Data.Where(order => order.OrderStatus == 7).ToList();
        
        AppendOrderSection(resultBuilder, "未配送", notDeliveredOrders);
        AppendOrderSection(resultBuilder, "配送中", deliveringOrders);
        AppendOrderSection(resultBuilder, "已完成", completedOrders);

        return resultBuilder.ToString();
    }

    public async Task<string> BuildCrmCustomerInfoByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var customerInfo = new StringBuilder();

        var  token = await _crmClient.GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var crmCustomers = await _crmClient.GetCustomersByPhoneNumberAsync(new GetCustmoersByPhoneNumberRequestDto { PhoneNumber = phoneNumber }, token, cancellationToken).ConfigureAwait(false);

            if (crmCustomers != null && crmCustomers.Any())
            {
                foreach (var customer in crmCustomers)
                {
                    customerInfo.AppendLine($"手机号 {phoneNumber}:");
                    customerInfo.AppendLine($"- SAP编号: {customer.SapId}");
                    customerInfo.AppendLine($"- 客户名称: {customer.CustomerName}");
                    customerInfo.AppendLine($"- 地址: {customer.Street}");
                    customerInfo.AppendLine($"- 仓库: {customer.Warehouse}");
                    customerInfo.AppendLine($"- 备注: {customer.HeaderNote1}");

                    if (customer.Contacts != null && customer.Contacts.Count > 0)
                    {
                        customerInfo.AppendLine(" 联系人信息：");
                        foreach (var c in customer.Contacts)
                        {
                            customerInfo.AppendLine($" - 姓名：{c.Name}，电话：{c.Phone}，身份：{c.Identity}，语言：{c.Language}");
                        }
                    }

                    customerInfo.AppendLine();
                }
            }
            else
            {
                customerInfo.AppendLine($"没有找到手机号 {phoneNumber} 的 CRM 客户信息");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Build CRM info failed for phone {PhoneNumber}", phoneNumber);
        }

        return customerInfo.ToString();
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