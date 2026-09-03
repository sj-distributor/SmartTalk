using System.Text;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Dto.Crm;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesService : IScopedDependency
{
    Task<string> BuildCustomerItemsStringAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task<string> BuildCustomerDeliveryProgressStringAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task<Dictionary<string, string>> BuildCustomerDeliveryProgressStringsAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task<string> BuildDeliveryProgressListAsync(List<string> customerIds, CancellationToken cancellationToken);

    Task<Dictionary<string, string>> BuildCustomerItemsStringsAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task<string> HandleOrderArrivalTimeList(List<string> customerIds, CancellationToken cancellationToken);

    Task<CrmCustomerPhoneKnowledgeDto> BuildCrmKnowledgeByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);

    Task<CrmCustomerPhoneKnowledgeDto> BuildCrmKnowledgeByPhoneAsync(string phoneNumber, string crmToken, CancellationToken cancellationToken);

    Task<string> BuildCrmCustomerInfoByPhoneAsync(string phoneNumber, string crmToken, CancellationToken cancellationToken);
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

    private static readonly Dictionary<string, string[]> TimezoneWarehouseMapping = new(StringComparer.Ordinal)
    {
        ["America/New_York"] = ["101A"],
        ["America/Los_Angeles"] = ["101D", "1050", "1060", "1070", "1200", "1250", "1400", "1450", "1600", "1800"],
        ["America/Chicago"] = ["101G", "101J", "102B"],
        ["America/Denver"] = ["101H", "102H"]
    };

    private static readonly Dictionary<string, string> WarehouseTimezoneLookup = TimezoneWarehouseMapping
        .SelectMany(mapping => mapping.Value.Select(warehouse => new { Warehouse = warehouse, Timezone = mapping.Key }))
        .ToDictionary(x => x.Warehouse, x => x.Timezone, StringComparer.OrdinalIgnoreCase);

    private static readonly char[] WarehouseCodeSeparators =
    [
        ' ', '\t', '\r', '\n', ',', '，', ';', '；', '/', '\\', '|', '、'
    ];

    private const int CustomerItemsQueryBatchSize = 10;

    private readonly ICrmClient _crmClient;
    private readonly ISalesClient _salesClient;

    public SalesService(ICrmClient crmClient, ISalesClient salesClient)
    {
        _crmClient = crmClient;
        _salesClient = salesClient;
    }
    
    public async Task<string> BuildCustomerItemsStringAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        var customerItems = await BuildCustomerItemsStringsAsync(soldToIds, cancellationToken).ConfigureAwait(false);
        var allItems = customerItems.Values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => x.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries))
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .Take(150)
            .ToList();

        return string.Join(Environment.NewLine, allItems);
    }

    public async Task<Dictionary<string, string>> BuildCustomerItemsStringsAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalizedSoldToIds = NormalizeSoldToIds(soldToIds);
        if (normalizedSoldToIds.Count == 0)
        {
            Log.Warning("BuildCustomerItemsStringAsync called with empty soldToIds");
            return result;
        }

        var customerMaterialOverviews = await GetCustomerMaterialOverviewInBatchesAsync(normalizedSoldToIds, cancellationToken)
            .ConfigureAwait(false);

        var materialOverviewByCustomer = customerMaterialOverviews
            .Where(x => !string.IsNullOrWhiteSpace(x.CustomerNumber))
            .GroupBy(x => BuildCustomerLookupKey(x.CustomerNumber), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var soldToId in normalizedSoldToIds)
        {
            var customerLookupKey = BuildCustomerLookupKey(soldToId);
            var materialOverview = materialOverviewByCustomer.GetValueOrDefault(customerLookupKey);
            var materialItems = materialOverview?.Items?
                .Where(x => !string.IsNullOrWhiteSpace(x.MaterialNumber) || !string.IsNullOrWhiteSpace(x.MaterialDescription))
                .ToList() ?? [];

            var habitLookup = materialOverview?.Level5Habits?
                .Where(x => !string.IsNullOrWhiteSpace(x.LevelCode5))
                .GroupBy(x => x.LevelCode5, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, CustomerMaterialLevel5HabitDto>(StringComparer.OrdinalIgnoreCase);

            string FormatItem(CustomerMaterialItemDto item)
            {
                var materialDesc = item.MaterialDescription ?? string.Empty;
                var parts = materialDesc?.Split('·') ?? Array.Empty<string>();
                var name = parts.Length > 4 ? $"{parts[0]}{parts[4]}" : parts.FirstOrDefault() ?? "";
                var brand = parts.Length > 1 ? parts[1] : "";
                var size = parts.Length > 3 ? parts[3] : "";

                string aliasText = "";

                if (!string.IsNullOrEmpty(item.LevelCode5) && habitLookup.TryGetValue(item.LevelCode5, out var habit))
                {
                    aliasText = habit.CustomerLikeNames != null && habit.CustomerLikeNames.Any()
                        ? string.Join(", ", habit.CustomerLikeNames.Select(n => n.CustomerLikeName))
                        : "";
                }

                return $"Item: {name}, Brand: {brand}, Size: {size}, Aliases: {aliasText}, status: {item.GoodsStatus ?? ""}, " +
                       $"baseUnit: {item.BaseUnit ?? ""}, salesUnit: {item.SalesUnit ?? ""}, weights: {item.Weight}, " +
                       $"placeOfOrigin: {item.PlaceOfOrigin ?? ""}, packing: {item.Packing ?? ""}, specifications: {item.Specifications ?? ""}, " +
                       $"ranks: {item.Rank ?? ""}, atr: {item.Atr}";
            }

            result[soldToId] = string.Join(Environment.NewLine, materialItems.Select(FormatItem).Distinct().Take(150));
        }

        return result;
    }

    private async Task<List<CustomerMaterialOverviewDto>> GetCustomerMaterialOverviewInBatchesAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        var result = new List<CustomerMaterialOverviewDto>();

        foreach (var batch in soldToIds.Chunk(CustomerItemsQueryBatchSize))
        {
            var response = await _salesClient
                .GetCustomerMaterialOverviewAsync(
                    new GetCustomerMaterialOverviewRequestDto { CustomerNumbers = batch.ToList() },
                    cancellationToken)
                .ConfigureAwait(false);

            if (response?.Code != 200)
            {
                Log.Warning("GetCustomerMaterialOverviewAsync returned non-success response. ResultCode: {ResultCode}, ResultMsg: {ResultMsg}", response?.Code, response?.Message);
                continue;
            }

            if (response?.Data != null)
                result.AddRange(response.Data);
        }

        return result;
    }

    private static List<string> NormalizeSoldToIds(IEnumerable<string> soldToIds)
    {
        return (soldToIds ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .GroupBy(BuildCustomerLookupKey, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private static string BuildCustomerLookupKey(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return string.Empty;

        var trimmed = customerId.Trim();
        if (!trimmed.All(char.IsDigit)) return trimmed.ToUpperInvariant();

        var withoutLeadingZeros = trimmed.TrimStart('0');
        return withoutLeadingZeros.Length == 0 ? "0" : withoutLeadingZeros;
    }
    
    public async Task<string> BuildCustomerDeliveryProgressStringAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        var deliveryProgressByCustomer = await BuildCustomerDeliveryProgressStringsAsync(soldToIds, cancellationToken)
            .ConfigureAwait(false);

        return string.Join(Environment.NewLine, deliveryProgressByCustomer.Values);
    }

    public async Task<Dictionary<string, string>> BuildCustomerDeliveryProgressStringsAsync(
        List<string> soldToIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalizedSoldToIds = NormalizeSoldToIds(soldToIds);
        if (normalizedSoldToIds.Count == 0)
        {
            Log.Warning("BuildCustomerDeliveryProgressStringsAsync called with empty soldToIds");
            return result;
        }

        foreach (var batch in normalizedSoldToIds.Chunk(CustomerItemsQueryBatchSize))
        {
            var batchSoldToIds = batch.ToList();
            var processedCustomerIds = batchSoldToIds.Select(id => "0000" + id).ToList();
            var deliveryProgressResponse = await _salesClient.GetOrderArrivalTimeAsync(
                new GetOrderArrivalTimeRequestDto { CustomerIds = processedCustomerIds },
                cancellationToken).ConfigureAwait(false);

            var ordersByCustomer = (deliveryProgressResponse?.Data ?? [])
                .Where(order => order != null)
                .GroupBy(order => BuildCustomerLookupKey(order.CustomerId), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var soldToId in batchSoldToIds)
            {
                var customerOrders = ordersByCustomer.GetValueOrDefault(BuildCustomerLookupKey(soldToId)) ?? [];
                result[soldToId] =
                    $"=== 客户 {soldToId} 配送进度 ==={Environment.NewLine}{BuildDeliveryProgressText(customerOrders)}";
            }
        }

        return result;
    }

    public async Task<string> BuildDeliveryProgressListAsync(List<string> customerIds, CancellationToken cancellationToken)
    {
        var processedCustomerIds = customerIds.Select(id => "0000" + id).ToList();

        var deliveryProgressResponse = await _salesClient.GetOrderArrivalTimeAsync(
            new GetOrderArrivalTimeRequestDto { CustomerIds = processedCustomerIds }, cancellationToken).ConfigureAwait(false);

        return BuildDeliveryProgressText(deliveryProgressResponse?.Data ?? []);
    }

    private string BuildDeliveryProgressText(List<GetOrderArrivalTimeDataDto> orders)
    {
        if (orders.Count == 0) return "这位客户暂时没有订单。";
        
        var resultBuilder = new StringBuilder();
        
        var notDeliveredOrders = orders.Where(order => new[] { 0, 1, 2, 3, 5, 6, 8 }.Contains(order.OrderStatus)).ToList();
        
        var deliveringOrders = orders.Where(order => order.OrderStatus == 4).ToList();
        
        var completedOrders = orders.Where(order => order.OrderStatus == 7).ToList();
        
        AppendOrderSection(resultBuilder, "未配送", notDeliveredOrders);
        AppendOrderSection(resultBuilder, "配送中", deliveringOrders);
        AppendOrderSection(resultBuilder, "已完成", completedOrders);

        return resultBuilder.ToString();
    }

    public async Task<string> HandleOrderArrivalTimeList(List<string> customerIds, CancellationToken cancellationToken)
    {
        return await BuildDeliveryProgressListAsync(customerIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CrmCustomerPhoneKnowledgeDto> BuildCrmKnowledgeByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        return await BuildCrmKnowledgeByPhoneAsync(phoneNumber, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CrmCustomerPhoneKnowledgeDto> BuildCrmKnowledgeByPhoneAsync(string phoneNumber, string crmToken, CancellationToken cancellationToken)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        var crmCustomers = await TryGetCrmCustomersByPhoneAsync(normalizedPhone, crmToken, cancellationToken).ConfigureAwait(false);
        var deliveryInfos = await TryGetDeliveryInfoByPhoneAsync(normalizedPhone, cancellationToken).ConfigureAwait(false);

        return new CrmCustomerPhoneKnowledgeDto
        {
            CustomerInfo = BuildCustomerInfoText(normalizedPhone, crmCustomers),
            DeliveryInfo = BuildDeliveryInfoText(crmCustomers, deliveryInfos, normalizedPhone)
        };
    }

    public async Task<string> BuildCrmCustomerInfoByPhoneAsync(string phoneNumber, string crmToken, CancellationToken cancellationToken)
    {
        var knowledge = await BuildCrmKnowledgeByPhoneAsync(phoneNumber, crmToken, cancellationToken).ConfigureAwait(false);
        return knowledge.CustomerInfo;
    }

    private async Task<List<GetCustomersPhoneNumberDataDto>> TryGetCrmCustomersByPhoneAsync(string normalizedPhone, string crmToken, CancellationToken cancellationToken)
    {
        try
        {
            var token = crmToken;
            if (string.IsNullOrWhiteSpace(token))
                token = await _crmClient.GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(token))
            {
                Log.Warning("CRM token is empty, phone: {PhoneNumber}", normalizedPhone);
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

    private static string BuildCustomerInfoText(string normalizedPhone, List<GetCustomersPhoneNumberDataDto> crmCustomers)
    {
        var customerInfo = new StringBuilder();
        customerInfo.AppendLine($"来电号码: {normalizedPhone}");

        if (!crmCustomers.Any())
        {
            customerInfo.AppendLine("- 客户ID识别状态: 未识别到CRM-SAP ID");
            customerInfo.AppendLine("- 建议回复: 可以先请客户提供客户编号或公司名称，再协助查询对应送货时间。");
            return customerInfo.ToString();
        }

        for (var i = 0; i < crmCustomers.Count; i++)
        {
            var customer = crmCustomers[i];
            customerInfo.AppendLine($"客户 {i + 1}:");
            AppendCustomerBaseInfo(customerInfo, customer, normalizedPhone);
            customerInfo.AppendLine();
        }

        return customerInfo.ToString();
    }

    private static string BuildDeliveryInfoText(
        List<GetCustomersPhoneNumberDataDto> crmCustomers,
        List<GetDeliveryInfoByPhoneNumberResponseDto> deliveryInfos,
        string normalizedPhone)
    {
        var deliveryInfo = new StringBuilder();

        if (!crmCustomers.Any())
        {
            deliveryInfo.AppendLine($"来电号码: {normalizedPhone}");
            deliveryInfo.AppendLine("- 配送信息状态: 未识别到CRM-SAP ID，无法匹配送货路线。");
            deliveryInfo.AppendLine("- 建议回复: 可以先请客户提供客户编号或公司名称，再协助查询对应送货时间。");
            return deliveryInfo.ToString();
        }

        var deliveryLookup = BuildDeliveryLookup(deliveryInfos);

        for (var i = 0; i < crmCustomers.Count; i++)
        {
            var customer = crmCustomers[i];
            deliveryInfo.AppendLine($"客户 {i + 1}:");
            deliveryInfo.AppendLine($"- SAP编号: {customer.SapId}");
            deliveryInfo.AppendLine($"- 客户名称: {customer.CustomerName}");

            var sapId = customer.SapId?.Trim();
            deliveryLookup.TryGetValue(sapId ?? string.Empty, out var routeInfos);
            AppendDeliveryRouteSummary(deliveryInfo, routeInfos);
            deliveryInfo.AppendLine();
        }

        return deliveryInfo.ToString();
    }

    private static Dictionary<string, List<GetDeliveryInfoByPhoneNumberResponseDto>> BuildDeliveryLookup(List<GetDeliveryInfoByPhoneNumberResponseDto> deliveryInfos)
    {
        return deliveryInfos
            .Where(x => !string.IsNullOrWhiteSpace(x.SapId))
            .GroupBy(x => x.SapId.Trim())
            .ToDictionary(x => x.Key, x => x.ToList());
    }

    private static void AppendCustomerBaseInfo(StringBuilder customerInfo, GetCustomersPhoneNumberDataDto customer, string normalizedPhone)
    {
        customerInfo.AppendLine($"- SAP编号: {customer.SapId}");
        customerInfo.AppendLine($"- 客户名称: {customer.CustomerName}");
        customerInfo.AppendLine($"- 地址: {customer.Street}");
        customerInfo.AppendLine($"- 仓库: {customer.Warehouse}");
        var timezone = ResolveWarehouseTimezone(customer.Warehouse);
        if (!string.IsNullOrWhiteSpace(timezone))
            customerInfo.AppendLine($"- 送货/截单时区: {timezone}");
        customerInfo.AppendLine($"- 备注: {customer.HeaderNote1}");

        if (customer.Contacts == null || customer.Contacts.Count == 0) return;

        var targetPhoneKey = NormalizePhoneKey(normalizedPhone);
        var matchingContacts = customer.Contacts
            .Where(c => NormalizePhoneKey(c.Phone) == targetPhoneKey)
            .ToList();

        if (matchingContacts.Count == 0) return;

        customerInfo.AppendLine("- 联系人信息:");
        foreach (var c in matchingContacts)
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

    private static string ResolveWarehouseTimezone(string warehouse)
    {
        var warehouseCodes = ExtractWarehouseCodes(warehouse);
        if (warehouseCodes.Count == 0) return null;

        var timezones = warehouseCodes
            .Select(code => WarehouseTimezoneLookup.TryGetValue(code, out var timezone) ? timezone : null)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return timezones.Count == 0 ? null : string.Join("、", timezones);
    }

    private static List<string> ExtractWarehouseCodes(string warehouse)
    {
        if (string.IsNullOrWhiteSpace(warehouse)) return [];

        return warehouse
            .Split(WarehouseCodeSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizePhone(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return phoneNumber;

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) return "+1" + digits;
        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal)) return "+" + digits;

        return phoneNumber.Trim();
    }

    private static string NormalizePhoneKey(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return string.Empty;

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal))
            digits = digits.Substring(1);

        if (digits.Length > 10)
            digits = digits.Substring(digits.Length - 10);

        return digits;
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
                    $"{i + 1}. 订单号码：{order.SalesOrderNumber} ，客户ID：{order.CustomerId} ，预计送到时间：{FormatUtcAsPst(order.EstimatedDeliveryTime)}");
            }
            builder.AppendLine();
        }
    }

    private static string FormatUtcAsPst(DateTime utcTime)
    {
        var specifiedUtcTime = utcTime.Kind == DateTimeKind.Utc
            ? utcTime
            : DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(specifiedUtcTime, PstTimeZone.Get()).ToString("yyyy-MM-dd HH:mm:ss");
    }

}
