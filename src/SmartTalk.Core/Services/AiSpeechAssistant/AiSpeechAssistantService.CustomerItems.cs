using System.Text.Json;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantService
{
    private const int MaxStoreScopedCustomerItemLines = 150;

    private void EnsureCustomerItemsTool(List<AiSpeechAssistantFunctionCall> functions, Domain.AISpeechAssistant.AiSpeechAssistant assistant)
    {
        if (assistant.ModelProvider is not (RealtimeAiProvider.OpenAi or RealtimeAiProvider.Azure)) return;
        if (functions.Any(x => x.Type == AiSpeechAssistantSessionConfigType.Tool && x.Name == OpenAiToolConstants.QueryCustomerItemsByStoreName)) return;

        var content = new
        {
            type = "function",
            name = OpenAiToolConstants.QueryCustomerItemsByStoreName,
            description = "Use when a customer asks about HiFood warehouse product information, stock, availability, or orderable goods for a specific store. The customer will usually provide a store name, not a customer id. This tool searches CRM by store name, intersects CRM results with the current assistant customer ids, and returns at most 150 scoped item lines.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    store_name = new
                    {
                        type = "string",
                        description = "The store or restaurant name mentioned by the customer."
                    },
                    product_name = new
                    {
                        type = "string",
                        description = "Optional product standard name or customer nickname mentioned by the customer."
                    }
                },
                required = new[] { "store_name" }
            }
        };

        functions.Add(new AiSpeechAssistantFunctionCall
        {
            AssistantId = assistant.Id,
            Name = OpenAiToolConstants.QueryCustomerItemsByStoreName,
            Content = JsonConvert.SerializeObject(content),
            Type = AiSpeechAssistantSessionConfigType.Tool,
            ModelProvider = assistant.ModelProvider,
            IsActive = true
        });
    }

    private async Task ProcessQueryCustomerItemsByStoreNameAsync(JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var args = ParseQueryCustomerItemsArguments(jsonDocument.GetProperty("arguments").ToString());
        var output = await BuildStoreScopedCustomerItemsOutputAsync(args, cancellationToken).ConfigureAwait(false);

        var message = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output
            }
        };

        await SendToWebSocketAsync(_openaiWebSocket, message, cancellationToken).ConfigureAwait(false);
        await SendToWebSocketAsync(_openaiWebSocket, new { type = "response.create" }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> BuildStoreScopedCustomerItemsOutputAsync(QueryCustomerItemsByStoreNameArguments args, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args.StoreName))
            return "Reply in the guest's language: Please ask the customer which store or restaurant name they are asking about before checking HiFood product information.";

        _aiSpeechAssistantStreamContext.CandidateCustomerIds = SplitAssistantCustomerIds(_aiSpeechAssistantStreamContext.Assistant?.Name);

        var match = await _salesCustomerMatchService
            .MatchStoreNameInCustomerScopeAsync(_aiSpeechAssistantStreamContext.Assistant?.Name, args.StoreName, cancellationToken)
            .ConfigureAwait(false);

        _aiSpeechAssistantStreamContext.MatchedCustomerIds = match.SoldToIds;

        if (match.SoldToIds.Count == 0)
        {
            Log.Information(
                "[AiAssistant] Store name did not match assistant customer scope. AssistantId: {AssistantId}, StoreName: {StoreName}, AssistantCustomerIds: {AssistantCustomerIds}, CrmCustomerIds: {CrmCustomerIds}",
                _aiSpeechAssistantStreamContext.Assistant?.Id, args.StoreName, _aiSpeechAssistantStreamContext.CandidateCustomerIds, match.CrmMatchedSoldToIds);

            return "Reply in the guest's language: I could not match that store name to the stores linked to this call. Please ask the customer for another store name, address, phone number, or contact name before answering product stock or warehouse questions.";
        }

        var caches = await _salesDataProvider
            .GetCustomerItemsCacheBySoldToIdsAsync(match.SoldToIds, cancellationToken)
            .ConfigureAwait(false);

        var itemLines = caches
            .Where(x => !string.IsNullOrWhiteSpace(x.CacheValue))
            .SelectMany(x => x.CacheValue.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries))
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxStoreScopedCustomerItemLines)
            .ToList();

        Log.Information(
            "[AiAssistant] Store scoped customer items resolved. AssistantId: {AssistantId}, StoreName: {StoreName}, ProductName: {ProductName}, MatchedCustomerIds: {MatchedCustomerIds}, ItemLineCount: {ItemLineCount}",
            _aiSpeechAssistantStreamContext.Assistant?.Id, args.StoreName, args.ProductName, match.SoldToIds, itemLines.Count);

        if (itemLines.Count == 0)
            return $"Matched customer IDs for store \"{args.StoreName}\": {string.Join(", ", match.SoldToIds)}.\nNo cached HiFood product information was found for these customer IDs. Reply in the guest's language and explain that no product data is available for this store right now.";

        return
            $"Store name: {args.StoreName}\n" +
            $"Customer IDs matched within this assistant: {string.Join(", ", match.SoldToIds)}\n" +
            $"Customer IDs returned by CRM for this store name: {string.Join(", ", match.CrmMatchedSoldToIds)}\n" +
            $"Requested product name or alias: {args.ProductName ?? string.Empty}\n" +
            $"HiFood product information scoped to the matched customer IDs, limited to {MaxStoreScopedCustomerItemLines} lines:\n" +
            string.Join(Environment.NewLine, itemLines) +
            "\n\nReply in the guest's language. Use only the scoped product information above. Do not mix in products from other customer IDs.";
    }

    private static QueryCustomerItemsByStoreNameArguments ParseQueryCustomerItemsArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson)) return new QueryCustomerItemsByStoreNameArguments();

        try
        {
            return JsonConvert.DeserializeObject<QueryCustomerItemsByStoreNameArguments>(argumentsJson) ?? new QueryCustomerItemsByStoreNameArguments();
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            Log.Warning(ex, "Failed to parse query customer items arguments: {ArgumentsJson}", argumentsJson);
            return new QueryCustomerItemsByStoreNameArguments();
        }
    }

    private static List<string> SplitAssistantCustomerIds(string assistantName)
    {
        if (string.IsNullOrWhiteSpace(assistantName)) return [];

        return assistantName
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeCustomerId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeCustomerId(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return string.Empty;

        var normalized = customerId.Trim().TrimStart('0');
        return string.IsNullOrWhiteSpace(normalized) ? "0" : normalized;
    }

    private sealed class QueryCustomerItemsByStoreNameArguments
    {
        [JsonProperty("store_name")]
        public string StoreName { get; set; }

        [JsonProperty("product_name")]
        public string ProductName { get; set; }
    }
}
