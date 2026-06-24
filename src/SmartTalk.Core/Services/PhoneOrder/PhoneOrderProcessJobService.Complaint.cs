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
        var matchResults = customerIds.Count > 0
            ? await BuildCustomerComplaintInvoiceMatchResultsAsync(complaint, customerIds, cancellationToken).ConfigureAwait(false)
            : new List<ComplaintCustomerInvoiceMatchResult>
            {
                new()
                {
                    MatchResult = MatchComplaintInvoiceOrders(complaint, new List<ComplaintInvoiceOrder>(), false)
                }
            };

        return FormatComplaintFeedbackSection(complaint, matchResults);
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

    private async Task<List<ComplaintCustomerInvoiceMatchResult>> BuildCustomerComplaintInvoiceMatchResultsAsync(
        ComplaintFeedbackExtraction complaint,
        List<string> customerIds,
        CancellationToken cancellationToken)
    {
        var customerOrderContexts = await BuildCustomerComplaintInvoiceOrderContextsAsync(customerIds, cancellationToken).ConfigureAwait(false);
        var results = customerOrderContexts.Select(x => new ComplaintCustomerInvoiceMatchResult
        {
            CustomerId = x.CustomerId,
            QueryCustomerId = x.QueryCustomerId,
            MatchResult = MatchComplaintInvoiceOrders(complaint, x.Orders, true)
        }).ToList();

        await ApplyLlmProductMatchResultsAsync(complaint, customerOrderContexts, results, cancellationToken).ConfigureAwait(false);

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

    private async Task ApplyLlmProductMatchResultsAsync(
        ComplaintFeedbackExtraction complaint,
        List<ComplaintCustomerInvoiceOrderContext> customerOrderContexts,
        List<ComplaintCustomerInvoiceMatchResult> matchResults,
        CancellationToken cancellationToken)
    {
        var products = (complaint.Products ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (products.Count == 0) return;

        var unmatchedContexts = customerOrderContexts
            .Where(context =>
            {
                var result = matchResults.FirstOrDefault(x => IsSameCustomerMatchTarget(x.CustomerId, x.QueryCustomerId, context.CustomerId, context.QueryCustomerId));
                return result?.MatchResult?.IsMatched != true && context.Orders.Count > 0;
            })
            .ToList();

        if (unmatchedContexts.Count == 0) return;

        var llmResults = await MatchComplaintInvoiceOrdersByLlmAsync(products, unmatchedContexts, cancellationToken).ConfigureAwait(false);
        foreach (var llmResult in llmResults)
        {
            if (llmResult.IsMatched != true) continue;

            var context = unmatchedContexts.FirstOrDefault(x =>
                IsSameCustomerMatchTarget(llmResult.CustomerId, llmResult.QueryCustomerId, x.CustomerId, x.QueryCustomerId));

            if (context == null) continue;

            var matchedInvoiceNumbers = (llmResult.MatchedInvoiceNumbers ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeInvoiceNumber)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var matchedOrders = context.Orders
                .Where(x => matchedInvoiceNumbers.Contains(NormalizeInvoiceNumber(x.InvNumber), StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (matchedOrders.Count == 0) continue;

            var matchResult = matchResults.FirstOrDefault(x =>
                IsSameCustomerMatchTarget(x.CustomerId, x.QueryCustomerId, context.CustomerId, context.QueryCustomerId));

            if (matchResult == null) continue;

            matchResult.MatchResult = ComplaintInvoiceMatchResult.Success(
                string.IsNullOrWhiteSpace(llmResult.Reason) ? "商品匹配（LLM判断）" : $"商品匹配（LLM判断：{llmResult.Reason}）",
                matchedOrders);
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

        return ComplaintInvoiceMatchResult.Fail("无法匹配订单");
    }

    private async Task<List<ComplaintLlmCustomerProductMatchResult>> MatchComplaintInvoiceOrdersByLlmAsync(
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
                            "你是一名订单商品匹配助手。请按客户判断投诉商品是否全部存在于同一张 invoice 的物料列表中。\n" +
                            "匹配时允许繁简体、常见中英文名称、产地、包装描述差异，但必须是同一种核心商品；品牌/规格如果明确冲突则不能匹配；不能因为都是大类商品就误判为同一商品。\n" +
                            "如果同一客户下有多张 invoice 都满足条件，可以返回多张。只返回 JSON，不要额外解释。\n" +
                            "{\n" +
                            "  \"results\": [\n" +
                            "    {\n" +
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

            return result?.Results ?? new List<ComplaintLlmCustomerProductMatchResult>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Complaint product LLM match failed. Products={Products}", products);
            return new List<ComplaintLlmCustomerProductMatchResult>();
        }
    }

    private static string FormatComplaintFeedbackSection(
        ComplaintFeedbackExtraction complaint,
        List<ComplaintCustomerInvoiceMatchResult> customerMatchResults)
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

        foreach (var customerMatchResult in customerMatchResults ?? new List<ComplaintCustomerInvoiceMatchResult>())
        {
            var matchResult = customerMatchResult.MatchResult ?? ComplaintInvoiceMatchResult.Fail("无法匹配订单");
            var customerLabel = BuildCustomerIdLabel(customerMatchResult.CustomerId, customerMatchResult.QueryCustomerId);

            if (!string.IsNullOrWhiteSpace(customerLabel))
                builder.AppendLine($"客户ID：{customerLabel}");

            if (matchResult.IsMatched)
            {
                builder.AppendLine($"匹配成功：已锁定 Invoice {string.Join("、", matchResult.MatchedOrders.Select(x => x.InvNumber).Distinct())}");
                builder.AppendLine($"命中规则：{matchResult.MatchReason}");
            }
            else
            {
                builder.AppendLine($"匹配失败：{matchResult.MatchReason}");
            }
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

        return $"{customerId}（查询ID：{queryCustomerId}）";
    }

    private static bool IsSameCustomerMatchTarget(
        string leftCustomerId,
        string leftQueryCustomerId,
        string rightCustomerId,
        string rightQueryCustomerId)
    {
        var hasCustomerId = !string.IsNullOrWhiteSpace(leftCustomerId) && !string.IsNullOrWhiteSpace(rightCustomerId);
        var hasQueryCustomerId = !string.IsNullOrWhiteSpace(leftQueryCustomerId) && !string.IsNullOrWhiteSpace(rightQueryCustomerId);

        if (hasCustomerId && hasQueryCustomerId)
            return string.Equals(leftCustomerId, rightCustomerId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(leftQueryCustomerId, rightQueryCustomerId, StringComparison.OrdinalIgnoreCase);

        return (hasCustomerId && string.Equals(leftCustomerId, rightCustomerId, StringComparison.OrdinalIgnoreCase)) ||
               (hasQueryCustomerId && string.Equals(leftQueryCustomerId, rightQueryCustomerId, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeInvoiceNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = Regex.Replace(value.Trim(), "[^A-Za-z0-9]", string.Empty);
        return normalized.TrimStart('0');
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

    private sealed class ComplaintCustomerInvoiceMatchResult
    {
        public string CustomerId { get; set; }

        public string QueryCustomerId { get; set; }

        public ComplaintInvoiceMatchResult MatchResult { get; set; }
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
        public List<ComplaintLlmCustomerProductMatchResult> Results { get; set; } = new();
    }

    private sealed class ComplaintLlmCustomerProductMatchResult
    {
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
