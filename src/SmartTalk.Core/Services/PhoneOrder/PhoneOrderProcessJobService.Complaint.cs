using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial class PhoneOrderProcessJobService
{
    private async Task<string> BuildComplaintFeedbackAnalysisSectionAsync(
        string reportText,
        Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportText)) return string.Empty;

        var complaint = await ExtractComplaintFeedbackAsync(reportText, cancellationToken).ConfigureAwait(false);
        var customerIds = ParseCustomerIds(aiSpeechAssistant?.Name);
        var orders = customerIds.Count > 0
            ? await GetComplaintInvoiceOrdersAsync(customerIds, 7, cancellationToken).ConfigureAwait(false)
            : new List<ComplaintInvoiceOrder>();
        var matchResult = MatchComplaintInvoiceOrders(complaint, orders, customerIds.Count > 0);

        return FormatComplaintFeedbackSection(complaint, matchResult);
    }

    private async Task<ComplaintFeedbackExtraction> ExtractComplaintFeedbackAsync(string reportText, CancellationToken cancellationToken)
    {
        var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var pacificNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pacificZone);

        try
        {
            var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
            {
                Messages = new List<CompletionsRequestMessageDto>
                {
                    new()
                    {
                        Role = "system",
                        Content = new CompletionsStringContent(
                            "你是一名货品事故/退货投诉录音结构化助手。请只从输入的电话分析报告中提取客户明确表达的信息，不要猜测。\n" +
                            $"当前业务日期为 {pacificNow:yyyy-MM-dd}，遇到昨天、前天、上周五等模糊时间时，请换算为 yyyy-MM-dd。\n" +
                            "只返回 JSON，不要额外解释。字段如下：\n" +
                            "{\n" +
                            "  \"invoiceNumbers\": [\"Invoice单号，可多个，没有则空数组\"],\n" +
                            "  \"products\": [\"投诉商品名称，可多个，没有则空数组\"],\n" +
                            "  \"problemDescription\": \"问题描述，例如破损/缺少/品质问题/退货/其他\",\n" +
                            "  \"affectedQuantity\": \"受影响数量，保留客户原始单位，没有则空字符串\",\n" +
                            "  \"deliveryDate\": \"送货日期，yyyy-MM-dd，没有则空字符串\",\n" +
                            "  \"deliveryDateText\": \"客户原始时间表达，例如昨天/前天，没有则空字符串\"\n" +
                            "}")
                    },
                    new()
                    {
                        Role = "user",
                        Content = new CompletionsStringContent($"电话分析报告：\n{reportText}\n\nJSON:")
                    }
                },
                Model = OpenAiModel.Gpt4o,
                ResponseFormat = new() { Type = "json_object" }
            }, cancellationToken).ConfigureAwait(false);

            var response = completionResult.Data.Response?.Trim();
            var result = JsonConvert.DeserializeObject<ComplaintFeedbackExtraction>(response ?? string.Empty);
            return result ?? new ComplaintFeedbackExtraction();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Extract complaint feedback failed.");
            return new ComplaintFeedbackExtraction();
        }
    }

    private async Task<List<ComplaintInvoiceOrder>> GetComplaintInvoiceOrdersAsync(List<string> customerIds, int daysWindow, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _salesClient.GetOrderInformationByCustomerIdAsync(
                new GetOrderInformationByCustomerIdRequestDto { CustomerIds = customerIds },
                cancellationToken).ConfigureAwait(false);

            return BuildComplaintInvoiceOrders(response?.Data, daysWindow);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Get complaint invoice orders failed. CustomerIds={CustomerIds}", string.Join(",", customerIds));
            return new List<ComplaintInvoiceOrder>();
        }
    }

    private static List<ComplaintInvoiceOrder> BuildComplaintInvoiceOrders(List<GetOrderInformationByCustomerIdItemDto> items, int daysWindow)
    {
        if (items == null || items.Count == 0) return new List<ComplaintInvoiceOrder>();

        var pacificToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PstTimeZone.Get()).Date;
        var startDate = pacificToday.AddDays(-daysWindow + 1);

        return items
            .Where(x => !string.IsNullOrWhiteSpace(x.InvNumber) &&
                        x.InvDate.HasValue &&
                        x.InvDate.Value.Date >= startDate &&
                        x.InvDate.Value.Date <= pacificToday)
            .GroupBy(x => x.InvNumber.Trim())
            .Select(g =>
            {
                var invDate = g.Where(x => x.InvDate.HasValue).Select(x => x.InvDate).FirstOrDefault();
                var materialNames = g.Select(x => x.MaterialName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new ComplaintInvoiceOrder
                {
                    InvNumber = g.Key,
                    InvDate = invDate,
                    MaterialNames = materialNames
                };
            })
            .OrderByDescending(x => x.InvDate ?? DateTime.MinValue)
            .ToList();
    }

    private static ComplaintInvoiceMatchResult MatchComplaintInvoiceOrders(
        ComplaintFeedbackExtraction complaint,
        List<ComplaintInvoiceOrder> orders,
        bool hasCustomerId)
    {
        if (!hasCustomerId)
            return ComplaintInvoiceMatchResult.Fail("无法匹配订单（未识别客户ID）");

        if (orders == null || orders.Count == 0)
            return ComplaintInvoiceMatchResult.Fail("无法匹配订单（未查询到近7天invoice）");

        var invoiceNumbers = (complaint.InvoiceNumbers ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeInvoiceNumber)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (invoiceNumbers.Count > 0)
        {
            var invoiceMatches = orders
                .Where(o => invoiceNumbers.Contains(NormalizeInvoiceNumber(o.InvNumber), StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (invoiceMatches.Count > 0)
                return ComplaintInvoiceMatchResult.Success("Invoice单号匹配", invoiceMatches);
        }

        var deliveryDate = ParseDate(complaint.DeliveryDate);
        if (deliveryDate.HasValue)
        {
            var dateMatches = orders
                .Where(o => o.InvDate.HasValue && o.InvDate.Value.Date == deliveryDate.Value.Date)
                .ToList();

            if (dateMatches.Count > 0)
                return ComplaintInvoiceMatchResult.Success("送货日期匹配", dateMatches);
        }

        var products = (complaint.Products ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (products.Count > 0)
        {
            var productMatch = orders.FirstOrDefault(order =>
                products.All(product => order.MaterialNames.Any(material => IsMaterialMatch(product, material))));

            if (productMatch != null)
                return ComplaintInvoiceMatchResult.Success("商品匹配", new List<ComplaintInvoiceOrder> { productMatch });
        }

        return ComplaintInvoiceMatchResult.Fail("无法匹配订单");
    }

    private static string FormatComplaintFeedbackSection(ComplaintFeedbackExtraction complaint, ComplaintInvoiceMatchResult matchResult)
    {
        static string JoinOrEmpty(IEnumerable<string> values)
        {
            var list = values?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
            return list.Count == 0 ? "未识别" : string.Join("、", list);
        }

        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("投诉结构化信息：");
        builder.AppendLine($"Invoice单号：{JoinOrEmpty(complaint.InvoiceNumbers)}");
        builder.AppendLine($"投诉商品：{JoinOrEmpty(complaint.Products)}");
        builder.AppendLine($"问题描述：{(string.IsNullOrWhiteSpace(complaint.ProblemDescription) ? "未识别" : complaint.ProblemDescription)}");
        builder.AppendLine($"受影响数量：{(string.IsNullOrWhiteSpace(complaint.AffectedQuantity) ? "未识别" : complaint.AffectedQuantity)}");
        builder.AppendLine($"送货日期：{(string.IsNullOrWhiteSpace(complaint.DeliveryDate) ? "未识别" : complaint.DeliveryDate)}");
        builder.AppendLine();
        builder.AppendLine("订单匹配结果：");

        if (matchResult.IsMatched)
        {
            builder.AppendLine($"匹配成功：已锁定 Invoice {string.Join("、", matchResult.MatchedOrders.Select(x => x.InvNumber).Distinct())}");
            builder.AppendLine($"命中规则：{matchResult.MatchReason}");
        }
        else
        {
            builder.AppendLine($"匹配失败：{matchResult.MatchReason}");
        }

        return builder.ToString().TrimEnd();
    }

    private static List<string> ParseCustomerIds(string assistantName)
    {
        return string.IsNullOrWhiteSpace(assistantName)
            ? new List<string>()
            : assistantName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
    }

    private static string NormalizeInvoiceNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = Regex.Replace(value.Trim(), "[^A-Za-z0-9]", string.Empty);
        return normalized.TrimStart('0');
    }

    private static string NormalizeText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim().ToLowerInvariant(), "\\s+", string.Empty);
    }

    private static bool IsMaterialMatch(string normalizedProduct, string materialName)
    {
        var normalizedMaterial = NormalizeText(materialName);
        return !string.IsNullOrWhiteSpace(normalizedProduct) &&
               !string.IsNullOrWhiteSpace(normalizedMaterial) &&
               (normalizedMaterial.Contains(normalizedProduct, StringComparison.OrdinalIgnoreCase) ||
                normalizedProduct.Contains(normalizedMaterial, StringComparison.OrdinalIgnoreCase));
    }

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParse(value, out var result) ? result.Date : null;
    }

    private sealed class ComplaintFeedbackExtraction
    {
        [JsonProperty("invoiceNumbers")]
        public List<string> InvoiceNumbers { get; set; } = new();

        [JsonProperty("products")]
        public List<string> Products { get; set; } = new();

        [JsonProperty("problemDescription")]
        public string ProblemDescription { get; set; }

        [JsonProperty("affectedQuantity")]
        public string AffectedQuantity { get; set; }

        [JsonProperty("deliveryDate")]
        public string DeliveryDate { get; set; }

        [JsonProperty("deliveryDateText")]
        public string DeliveryDateText { get; set; }
    }

    private sealed class ComplaintInvoiceOrder
    {
        public string InvNumber { get; set; }

        public DateTime? InvDate { get; set; }

        public List<string> MaterialNames { get; set; } = new();
    }

    private sealed class ComplaintInvoiceMatchResult
    {
        public bool IsMatched { get; set; }

        public string MatchReason { get; set; }

        public List<ComplaintInvoiceOrder> MatchedOrders { get; set; } = new();

        public static ComplaintInvoiceMatchResult Success(string reason, List<ComplaintInvoiceOrder> orders)
        {
            return new ComplaintInvoiceMatchResult
            {
                IsMatched = true,
                MatchReason = reason,
                MatchedOrders = orders
            };
        }

        public static ComplaintInvoiceMatchResult Fail(string reason)
        {
            return new ComplaintInvoiceMatchResult
            {
                IsMatched = false,
                MatchReason = reason
            };
        }
    }
}
