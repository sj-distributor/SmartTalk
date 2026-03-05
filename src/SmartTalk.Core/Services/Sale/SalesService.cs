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
    private static readonly Dictionary<string, string> WeekdayMap = new()
    {
        ["1"] = "周一",
        ["2"] = "周二",
        ["3"] = "周三",
        ["4"] = "周四",
        ["5"] = "周五",
        ["6"] = "周六",
        ["7"] = "周日"
    };

    private readonly ICrmClient _crmClient;
    private readonly ISalesClient _salesClient;

    public SalesService(ICrmClient crmClient, ISalesClient salesClient)
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
        }

        return string.Join(Environment.NewLine, allItems.Distinct().Take(150));
    }
    
    public async Task<string> HandleOrderArrivalTimeList(List<string> customerIds, CancellationToken cancellationToken)
    {
        var processedCustomerIds = customerIds.Select(id => "0000" + id).ToList();

        var getOrderArrivalTimeList = await _salesClient.GetOrderArrivalTimeAsync(
            new GetOrderArrivalTimeRequestDto { CustomerIds = processedCustomerIds }, cancellationToken).ConfigureAwait(false);

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
        var normalizedPhone = NormalizePhone(phoneNumber);
        var customerInfo = new StringBuilder();
        var crmCustomers = await TryGetCrmCustomersByPhoneAsync(normalizedPhone, cancellationToken).ConfigureAwait(false);
        var deliveryInfos = await TryGetDeliveryInfoByPhoneAsync(normalizedPhone, cancellationToken).ConfigureAwait(false);

        customerInfo.AppendLine($"来电号码: {normalizedPhone}");

        if (!crmCustomers.Any())
        {
            customerInfo.AppendLine("- 客户ID识别状态: 未识别到CRM-SAP ID");
            customerInfo.AppendLine("- 建议回复: 可以先请客户提供客户编号或公司名称，再协助查询对应送货时间。");
            return customerInfo.ToString();
        }

        var deliveryLookup = BuildDeliveryLookup(deliveryInfos);

        for (var i = 0; i < crmCustomers.Count; i++)
        {
            var customer = crmCustomers[i];
            customerInfo.AppendLine($"客户 {i + 1}:");
            AppendCustomerBaseInfo(customerInfo, customer);
            var sapId = customer.SapId?.Trim();
            deliveryLookup.TryGetValue(sapId ?? string.Empty, out var routeInfos);
            AppendDeliveryRouteSummary(customerInfo, routeInfos);
            customerInfo.AppendLine();
        }

        return customerInfo.ToString();
    }

    private async Task<List<GetCustomersPhoneNumberDataDto>> TryGetCrmCustomersByPhoneAsync(string normalizedPhone, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _crmClient.GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                Log.Warning("BuildCrmCustomerInfoByPhoneAsync: CRM token is empty, phone: {PhoneNumber}", normalizedPhone);
                return [];
            }

            return await _crmClient.GetCustomersByPhoneNumberAsync(
                    new GetCustmoersByPhoneNumberRequestDto { PhoneNumber = normalizedPhone },
                    token, cancellationToken)
                .ConfigureAwait(false) ?? [];
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Build CRM customer basic info failed for phone {PhoneNumber}", normalizedPhone);
            return [];
        }
    }

    private static Dictionary<string, List<GetDeliveryInfoByPhoneNumberResponseDto>> BuildDeliveryLookup(List<GetDeliveryInfoByPhoneNumberResponseDto> deliveryInfos)
    {
        return deliveryInfos
            .Where(x => !string.IsNullOrWhiteSpace(x.SapId))
            .GroupBy(x => x.SapId.Trim())
            .ToDictionary(x => x.Key, x => x.ToList());
    }

    private static void AppendCustomerBaseInfo(StringBuilder customerInfo, GetCustomersPhoneNumberDataDto customer)
    {
        customerInfo.AppendLine($"- SAP编号: {customer.SapId}");
        customerInfo.AppendLine($"- 客户名称: {customer.CustomerName}");
        customerInfo.AppendLine($"- 地址: {customer.Street}");
        customerInfo.AppendLine($"- 仓库: {customer.Warehouse}");
        customerInfo.AppendLine($"- 备注: {customer.HeaderNote1}");

        if (customer.Contacts == null || customer.Contacts.Count == 0) return;

        customerInfo.AppendLine("- 联系人信息:");
        foreach (var c in customer.Contacts)
            customerInfo.AppendLine($"  - 姓名: {c.Name}，电话: {c.Phone}，身份: {c.Identity}，语言: {c.Language}");
    }

    private async Task<List<GetDeliveryInfoByPhoneNumberResponseDto>> TryGetDeliveryInfoByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        try
        {
            var routes = await _crmClient.GetDeliveryInfoByPhoneNumberAsync(phoneNumber, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (routes == null || routes.Count == 0) return [];

            Log.Information("CRM delivery route info found. QueryPhone: {QueryPhone}, Count: {Count}", phoneNumber, routes.Count);
            return routes;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Get CRM delivery info by phone failed. QueryPhone: {QueryPhone}", phoneNumber);
            return [];
        }
    }

    private static void AppendDeliveryRouteSummary(StringBuilder customerInfo, List<GetDeliveryInfoByPhoneNumberResponseDto> routeInfos)
    {
        if (routeInfos == null || routeInfos.Count == 0)
        {
            customerInfo.AppendLine("- 路线状态: 未配置路线");
            return;
        }

        for (var i = 0; i < routeInfos.Count; i++)
        {
            var routeIndex = i + 1;
            var routeInfo = routeInfos[i];
            var routeName = routeInfo.RouteName?.Trim();
            if (string.IsNullOrWhiteSpace(routeName))
            {
                customerInfo.AppendLine($"- 路线{routeIndex}: 未配置路线");
                continue;
            }

            customerInfo.AppendLine($"- 路线{routeIndex}: {routeName}");

            var deliveryDaysText = FormatDeliveryDays(routeInfo.DeliveryTime, out var hasConfiguredDays);
            var deliveryWindow = FormatDeliveryWindow(routeInfo.EntryTime, routeInfo.LeaveTime, out var hasConfiguredWindow);

            if (hasConfiguredDays)
            {
                customerInfo.AppendLine(hasConfiguredWindow
                    ? $"  送货安排: 每{deliveryDaysText} {deliveryWindow}"
                    : $"  送货安排: 每{deliveryDaysText}");
            }
            else
            {
                customerInfo.AppendLine($"  送货安排: 未配置（原始值: {deliveryDaysText}）");
                if (!string.IsNullOrWhiteSpace(routeInfo.EntryTime) || !string.IsNullOrWhiteSpace(routeInfo.LeaveTime))
                    customerInfo.AppendLine($"  送货时段: {deliveryWindow}");
                customerInfo.AppendLine("  建议回复: 目前还在确认您所在路线的送货时间，建议转接人工客服协助确认。");
            }
        }
    }

    private static string FormatDeliveryDays(string deliveryTime, out bool hasConfiguredDays)
    {
        hasConfiguredDays = false;

        if (string.IsNullOrWhiteSpace(deliveryTime))
            return "空值";

        var original = deliveryTime.Trim();
        var normalized = original.Replace("，", ",").Replace("、", ",").Replace(" ", "");

        var tokens = normalized.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Distinct().ToList();

        if (tokens.Count == 0)
            return original;

        // Only numeric weekday tokens (1-7) are treated as configured delivery days.
        var mappedDays = tokens.Select(token => WeekdayMap.TryGetValue(token, out var day) ? day : null).ToList();

        if (mappedDays.Any(x => string.IsNullOrWhiteSpace(x)))
            return original;

        hasConfiguredDays = true;
        return string.Join("、", mappedDays!);
    }

    private static string FormatDeliveryWindow(string entryTime, string leaveTime, out bool hasConfiguredWindow)
    {
        var entry = entryTime?.Trim();
        var leave = leaveTime?.Trim();

        hasConfiguredWindow = !string.IsNullOrWhiteSpace(entry) && !string.IsNullOrWhiteSpace(leave);

        if (string.IsNullOrWhiteSpace(entry) && string.IsNullOrWhiteSpace(leave))
            return "未配置";

        return $"{entry ?? "未配置"}-{leave ?? "未配置"}";
    }

    private static string NormalizePhone(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return phoneNumber;

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) return "+1" + digits;
        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal)) return "+" + digits;

        return phoneNumber.Trim();
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
