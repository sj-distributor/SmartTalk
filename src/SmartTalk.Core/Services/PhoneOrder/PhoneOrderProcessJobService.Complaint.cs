using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial class PhoneOrderProcessJobService
{
    private async Task<string> BuildComplaintFeedbackAnalysisSectionAsync(
        string reportText,
        Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant,
        PhoneOrderRecord record,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportText)) return string.Empty;

        var complaint = await ExtractComplaintFeedbackAsync(reportText, cancellationToken).ConfigureAwait(false);
        var customerIds = ParseCustomerIds(aiSpeechAssistant?.Name);
        var matchResults = customerIds.Count > 0
            ? await MatchProductsToCustomersAsync(complaint, customerIds, cancellationToken).ConfigureAwait(false)
            : new List<ProductCustomerMatchResult>
            {
                new() { CustomerId = "未知", IsMatched = false, MatchReason = "无法匹配订单（未识别客户ID）" }
            };

        return FormatPerProductComplaintSection(complaint, matchResults, record);
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

    private async Task<List<ProductCustomerMatchResult>> MatchProductsToCustomersAsync(
        ComplaintFeedbackExtraction complaint,
        List<string> customerIds,
        CancellationToken cancellationToken)
    {
        var products = (complaint.Products ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (products.Count == 0) return new List<ProductCustomerMatchResult>();

        var customerOrderContexts = await BuildCustomerComplaintInvoiceOrderContextsAsync(customerIds, cancellationToken).ConfigureAwait(false);
        var results = products.Select(p => new ProductCustomerMatchResult { ProductName = p }).ToList();

        if (customerOrderContexts.Any(c => c.Orders.Count > 0))
        {
            var llmResults = await MatchProductsByLlmAsync(products, customerOrderContexts, cancellationToken).ConfigureAwait(false);
            foreach (var llm in llmResults)
            {
                var r = results.FirstOrDefault(x => string.Equals(x.ProductName, llm.ProductName, StringComparison.OrdinalIgnoreCase));
                if (r == null) continue;

                r.CustomerId = llm.CustomerId;
                r.QueryCustomerId = llm.QueryCustomerId;
                r.IsMatched = llm.IsMatched;
                r.MatchedInvoiceNumber = llm.MatchedInvoiceNumbers?.FirstOrDefault();
                r.MatchReason = llm.Reason;
            }
        }

        return results;
    }

    private async Task<List<ComplaintCustomerInvoiceOrderContext>> BuildCustomerComplaintInvoiceOrderContextsAsync(
        List<string> customerIds,
        CancellationToken cancellationToken)
    {
        var tasks = customerIds.Select(async customerId =>
        {
            var queryCustomerId = NormalizeCustomerIdForOrderQuery(customerId);
            var orders = await GetComplaintInvoiceOrdersAsync(customerId, queryCustomerId, cancellationToken).ConfigureAwait(false);

            return new ComplaintCustomerInvoiceOrderContext
            {
                CustomerId = customerId,
                QueryCustomerId = queryCustomerId,
                Orders = orders
            };
        });

        return (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();
    }

    private async Task<List<ComplaintLlmProductMatchResult>> MatchProductsByLlmAsync(
        List<string> products,
        List<ComplaintCustomerInvoiceOrderContext> customerOrderContexts,
        CancellationToken cancellationToken)
    {
        try
        {
            var customerPayload = customerOrderContexts.Select(customer => new
            {
                customerId = customer.CustomerId,
                queryCustomerId = customer.QueryCustomerId,
                orders = customer.Orders.Select(order => new
                {
                    invNumber = order.InvNumber,
                    invDate = order.InvDate?.ToString("yyyy-MM-dd"),
                    materialNames = order.MaterialNames
                }).ToList()
            }).ToList();

            var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
            {
                Messages = new List<CompletionsRequestMessageDto>
                {
                    new()
                    {
                        Role = "system",
                        Content = new CompletionsStringContent(
                            "你是一名订单商品匹配助手。请逐个判断每个投诉商品分别由哪个客户的哪张 invoice 配送。\n" +
                            "匹配时允许繁简体、常见中英文名称、产地、包装描述差异，但必须是同一种核心商品；品牌/规格如果明确冲突则不能匹配；不能因为都是大类商品就误判为同一商品。\n" +
                            "对于每个投诉商品，最多匹配到一个客户的一张 invoice。如果无法确定归属，isMatched 设为 false。\n" +
                            "只返回 JSON，不要额外解释。\n" +
                            "{\n" +
                            "  \"results\": [\n" +
                            "    {\n" +
                            "      \"productName\": \"投诉商品名称\",\n" +
                            "      \"customerId\": \"原始客户ID\",\n" +
                            "      \"queryCustomerId\": \"查询客户ID\",\n" +
                            "      \"isMatched\": true,\n" +
                            "      \"matchedInvoiceNumbers\": [\"匹配到的invoice单号\"],\n" +
                            "      \"reason\": \"简短中文原因\"\n" +
                            "    }\n" +
                            "  ]\n" +
                            "}")
                    },
                    new()
                    {
                        Role = "user",
                        Content = new CompletionsStringContent(
                            "投诉商品：\n" + JsonConvert.SerializeObject(products) + "\n\n" +
                            "候选客户订单：\n" + JsonConvert.SerializeObject(customerPayload) + "\n\nJSON:")
                    }
                },
                Model = OpenAiModel.Gpt4o,
                ResponseFormat = new() { Type = "json_object" }
            }, cancellationToken).ConfigureAwait(false);

            var response = completionResult.Data.Response?.Trim();
            var result = JsonConvert.DeserializeObject<ComplaintLlmProductMatchResponse>(response ?? string.Empty);

            Log.Information(
                "Complaint product LLM match result. Products={Products}, ResultCount={ResultCount}, Results={Results}",
                products,
                result?.Results?.Count ?? 0,
                result?.Results);

            return result?.Results ?? new List<ComplaintLlmProductMatchResult>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Complaint product LLM match failed. Products={Products}", products);
            return new List<ComplaintLlmProductMatchResult>();
        }
    }

    private async Task<List<ComplaintInvoiceOrder>> GetComplaintInvoiceOrdersAsync(
        string customerId,
        string queryCustomerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(queryCustomerId)) return new List<ComplaintInvoiceOrder>();

        try
        {
            var response = await _salesClient.GetOrderInformationByCustomerIdAsync(
                new GetOrderInformationByCustomerIdRequestDto { CustomerIds = new List<string> { queryCustomerId } },
                cancellationToken).ConfigureAwait(false);

            var orders = BuildComplaintInvoiceOrders(response?.Data);
            Log.Information(
                "Get complaint invoice orders succeeded. CustomerId={CustomerId}, QueryCustomerId={QueryCustomerId}, RawItemCount={RawItemCount}, InvoiceCount={InvoiceCount}, Orders={Orders}",
                customerId,
                queryCustomerId,
                response?.Data?.Count ?? 0,
                orders.Count,
                orders.Select(x => new
                {
                    x.InvNumber,
                    InvDate = x.InvDate?.ToString("yyyy-MM-dd"),
                    x.MaterialNames
                }).ToList());

            return orders;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Get complaint invoice orders failed. CustomerId={CustomerId}, QueryCustomerId={QueryCustomerId}", customerId, queryCustomerId);
            return new List<ComplaintInvoiceOrder>();
        }
    }

    private static List<ComplaintInvoiceOrder> BuildComplaintInvoiceOrders(List<GetOrderInformationByCustomerIdItemDto> items)
    {
        if (items == null || items.Count == 0) return new List<ComplaintInvoiceOrder>();

        return items
            .Where(x => !string.IsNullOrWhiteSpace(x.InvNumber))
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

    private static string FormatPerProductComplaintSection(
        ComplaintFeedbackExtraction complaint,
        List<ProductCustomerMatchResult> productMatchResults,
        PhoneOrderRecord record)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("投訴信息：");

        var grouped = (productMatchResults ?? new List<ProductCustomerMatchResult>())
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CustomerId) ? "未知" : x.CustomerId)
            .ToList();

        for (var g = 0; g < grouped.Count; g++)
        {
            var group = grouped.ToList()[g];
            var first = group.First();
            var customerLabel = BuildCustomerIdLabel(first.CustomerId, first.QueryCustomerId);

            if (!string.IsNullOrWhiteSpace(customerLabel))
                builder.AppendLine($"客户ID:{customerLabel}");
            else
                builder.AppendLine("客户ID:未知");

            var complaintIndex = 1;
            foreach (var productResult in group)
            {
                var callTime = record?.CreatedDate.ToString("yyyy-MM-dd HH:mm") ?? "";
                builder.AppendLine($"  ─ 投诉单 {complaintIndex++}（{callTime}）");
                builder.AppendLine($"    Invoice单号:{(productResult.IsMatched && !string.IsNullOrWhiteSpace(productResult.MatchedInvoiceNumber) ? productResult.MatchedInvoiceNumber : "暫無")}");
                builder.AppendLine($"    投诉商品:{productResult.ProductName}");
                builder.AppendLine($"    问题描述:{(!string.IsNullOrWhiteSpace(complaint.ProblemDescription) ? complaint.ProblemDescription : "暫無")}");
                builder.AppendLine($"    受影响数量:{(!string.IsNullOrWhiteSpace(complaint.AffectedQuantity) ? complaint.AffectedQuantity : "暫無")}");
                builder.AppendLine($"    送货日期:{(!string.IsNullOrWhiteSpace(complaint.DeliveryDate) ? complaint.DeliveryDate : "暫無")}");

                if (productResult.IsMatched)
                {
                    builder.AppendLine($"    匹配成功:已锁定Invoice {productResult.MatchedInvoiceNumber}");
                    builder.AppendLine($"    命中规则:{productResult.MatchReason}");
                }
                else
                {
                    builder.AppendLine($"    匹配失败：{productResult.MatchReason}");
                }
            }

            if (g < grouped.Count - 1)
                builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static List<string> ParseCustomerIds(string assistantName)
    {
        return string.IsNullOrWhiteSpace(assistantName)
            ? new List<string>()
            : assistantName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
    }

    private static string NormalizeCustomerIdForOrderQuery(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return string.Empty;

        var trimmed = customerId.Trim();
        return char.IsDigit(trimmed[0]) && trimmed.Length < 10 ? trimmed.PadLeft(10, '0') : trimmed;
    }

    private static string BuildCustomerIdLabel(string customerId, string queryCustomerId)
    {
        if (string.IsNullOrWhiteSpace(customerId) && string.IsNullOrWhiteSpace(queryCustomerId))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(customerId) ||
            string.Equals(customerId, queryCustomerId, StringComparison.OrdinalIgnoreCase))
            return queryCustomerId;

        return $"{customerId}(查询ID:{queryCustomerId})";
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

    private sealed class ComplaintCustomerInvoiceOrderContext
    {
        public string CustomerId { get; set; }

        public string QueryCustomerId { get; set; }

        public List<ComplaintInvoiceOrder> Orders { get; set; } = new();
    }

    private sealed class ComplaintLlmProductMatchResponse
    {
        [JsonProperty("results")]
        public List<ComplaintLlmProductMatchResult> Results { get; set; } = new();
    }

    private sealed class ComplaintLlmProductMatchResult
    {
        [JsonProperty("productName")]
        public string ProductName { get; set; }

        [JsonProperty("customerId")]
        public string CustomerId { get; set; }

        [JsonProperty("queryCustomerId")]
        public string QueryCustomerId { get; set; }

        [JsonProperty("isMatched")]
        public bool IsMatched { get; set; }

        [JsonProperty("matchedInvoiceNumbers")]
        public List<string> MatchedInvoiceNumbers { get; set; } = new();

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }

    private sealed class ProductCustomerMatchResult
    {
        public string ProductName { get; set; }

        public string CustomerId { get; set; }

        public string QueryCustomerId { get; set; }

        public bool IsMatched { get; set; }

        public string MatchedInvoiceNumber { get; set; }

        public string MatchReason { get; set; }
    }
}
