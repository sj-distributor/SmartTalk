using System.Text;
using Serilog;
using SmartTalk.Messages.Dto.Crm;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.Sale;

public class SalesService
{
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
                       $"ranks: {partInfo?.Ranks ?? ""}, atr: {partInfo?.Atr ?? 0}"; 
            } 
            
            allItems.AddRange(askItems.Select(x => FormatItem(x.MaterialDesc, x.LevelCode, x.Material))); 
            allItems.AddRange(orderItems.Select(x => FormatItem(x.MaterialDescription, x.LevelCode, x.MaterialNumber))); 
            
            var customerOrderArrivalText = await HandleOrderArrivalTimeList(new List<string> { soldToId }, cancellationToken);
            if (!string.IsNullOrEmpty(customerOrderArrivalText))
            {
                allItems.Add($"=== 客户 {soldToId} 订单到货信息 ===");
                allItems.Add(customerOrderArrivalText);
            }
            
            var crmInfo = await BuildCrmCustomerInfoAsync(soldToId, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(crmInfo))
            {
                allItems.Add("=== Customer Info ===");
                allItems.Add(crmInfo);
            }
        }
        
        return string.Join(Environment.NewLine, allItems);
    }

    public async Task<string> BuildCrmCustomerInfoAsync(string soldToId, CancellationToken cancellationToken)
    {
        var customerInfo = new StringBuilder();

        try
        {
            var contacts = await _crmClient.GetCustomerContactsAsync(soldToId, cancellationToken).ConfigureAwait(false);
            if (contacts != null && contacts.Any())
            {
                customerInfo.AppendLine("Contacts:");
                foreach (var contact in contacts)
                {
                    customerInfo.AppendLine(
                        $"- Name: {contact.Name}, Phone: {contact.Phone}, Identity: {contact.Identity}, Language: {contact.Language}");
                }
            }
            else
            {
                customerInfo.AppendLine($"No contacts found for CustomerId: {soldToId}");
            }

            var phones = contacts?.Where(c => !string.IsNullOrEmpty(c.Phone)).Select(c => c.Phone).ToList() ?? new List<string>();
            foreach (var phone in phones)
            {
                var crmCustomers = await _crmClient.GetCustomersByPhoneNumberAsync(new GetCustmoersByPhoneNumberRequestDto { PhoneNumber = phone }, cancellationToken).ConfigureAwait(false);

                if (crmCustomers != null && crmCustomers.Any())
                {
                    foreach (var customer in crmCustomers)
                    {
                        customerInfo.AppendLine($"Customer Info for phone {phone}:");
                        customerInfo.AppendLine($"- SAP ID: {customer.SapId}");
                        customerInfo.AppendLine($"- Name: {customer.CustomerName}");
                        customerInfo.AppendLine($"- Street: {customer.Street}");
                        customerInfo.AppendLine($"- Warehouse: {customer.Warehouse}");
                        customerInfo.AppendLine($"- HeaderNote1: {customer.HeaderNote1}");

                        if (customer.Contacts != null && customer.Contacts.Count > 0)
                        {
                            customerInfo.AppendLine("  Customer Contacts:");
                            foreach (var c in customer.Contacts)
                            {
                                customerInfo.AppendLine($"  - Name: {c.Name}, Phone: {c.Phone}, Identity: {c.Identity}, Language: {c.Language}");
                            }
                        }
                    }
                }
                else
                {
                    customerInfo.AppendLine($"No CRM customer info found for phone {phone}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to build CRM info for CustomerId: {CustomerId}", soldToId);
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